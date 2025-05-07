using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const double MaxSpeed = 100.0;               // Velocidad máxima de crucero
        const double gyrosThr = 0.001;               //Precisión de alineación
        const double gyrosSpeed = 2f;                //Velocidad de los giroscopios
        const double CollisionDetectRange = 1000.0;  // Rango de detección de colisiones
        const double LateralAvoidDistance = 1000.0;  // Distancia a recorrer al esquivar
        const string RemoteName = "Stingray Remote Control";  // Nombre del remote control
        const string CameraName = "Stingray Camera";  // Nombre del remote control
        const string BeaconName = "Stingray Distress Beacon"; // Nombre del beacon

        enum NavState
        {
            Idle, Accelerating, Cruising, Avoiding, Returning, Distress
        }

        readonly IMyRemoteControl remote;
        readonly IMyCameraBlock camera;
        readonly List<IMyThrust> thrusters;
        readonly List<IMyGyro> gyros;
        readonly IMyBeacon beacon;

        NavState currentState = NavState.Idle;
        Vector3D destination;
        Vector3D startPosition;
        Vector3D lateralOffset;
        Vector3D savedVelocity;
        bool hasTarget = false;
        Vector3D originalDirection;
        bool lateralDirectionSet = false;

        MyDetectedEntityInfo lastHit;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            remote = GetBlockWithName<IMyRemoteControl>(RemoteName);
            camera = GetBlockWithName<IMyCameraBlock>(CameraName);
            beacon = GetBlockWithName<IMyBeacon>(BeaconName);
            thrusters = GetBlocksOfType<IMyThrust>();
            gyros = GetBlocksOfType<IMyGyro>();

            LoadState();
        }

        void LoadState()
        {
            if (Storage.Length == 0) return;

            var parts = Storage.Split('|');
            if (parts.Length >= 6)
            {
                currentState = (NavState)StrToInt(parts[0]);
                destination = StrToVec(parts[1]);
                startPosition = StrToVec(parts[2]);
                lateralOffset = StrToVec(parts[3]);
                savedVelocity = StrToVec(parts[4]);
                hasTarget = StrToInt(parts[5]) == 1;
            }
        }
        void SaveState()
        {
            var parts = new List<string>
            {
                $"{(int)currentState}",
                $"{VecToStr(destination)}",
                $"{VecToStr(startPosition)}",
                $"{VecToStr(lateralOffset)}",
                $"{VecToStr(savedVelocity)}",
                $"{(hasTarget?1:0)}",
            };
            Storage = string.Join("|", parts);
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument.StartsWith("NAV|"))
            {
                destination = StrToVec(argument.Substring(4));
                startPosition = remote.GetPosition();
                originalDirection = Vector3D.Normalize(destination - startPosition);
                currentState = NavState.Accelerating;
                hasTarget = true;
                lateralDirectionSet = false;
                BroadcastStatus("Starting navigation.");
                SaveState();

                return;
            }

            if (!hasTarget || currentState == NavState.Idle)
            {
                Echo("Waiting for position...");
                return;
            }

            Echo($"{currentState} - {lastHit.HitPosition ?? Vector3D.Zero}");
            switch (currentState)
            {
                case NavState.Accelerating:
                    Accelerate();
                    break;
                case NavState.Cruising:
                    Cruise();
                    break;
                case NavState.Avoiding:
                    Avoid();
                    break;
                case NavState.Returning:
                    ReturnToPath();
                    break;
                case NavState.Distress:
                    Distress();
                    break;
            }

            SaveState();
        }

        // === ESTADOS ===

        void Accelerate()
        {
            var toTarget = Vector3D.Normalize(destination - remote.GetPosition());
            var velocity = remote.GetShipVelocities().LinearVelocity;

            if (IsObstacleAhead(toTarget))
            {
                StopAllThrust();
                StopGyros();
                currentState = NavState.Avoiding;
                BroadcastStatus("Obstacle detected. Avoiding...");
                return;
            }

            if (velocity.Length() >= MaxSpeed * 0.95 && VectorAligned(velocity, toTarget))
            {
                StopAllThrust();
                StopGyros();
                currentState = NavState.Cruising;
                BroadcastStatus("Reached cruise speed.");
                return;
            }

            ApplyThrust(toTarget * 100000);
            AlignToDirection(toTarget);
        }
        void Cruise()
        {
            var toTarget = destination - remote.GetPosition();
            double distance = toTarget.Length();
            var direction = Vector3D.Normalize(toTarget);
            var velocity = remote.GetShipVelocities().LinearVelocity;

            if (IsObstacleAhead(direction))
            {
                StopAllThrust();
                StopGyros();
                currentState = NavState.Avoiding;
                BroadcastStatus("Collision ahead. Avoiding...");
                return;
            }

            if (distance < 50)
            {
                StopAllThrust();
                StopGyros();
                BroadcastStatus("Destination reached.");
                currentState = NavState.Idle;
                return;
            }

            AlignToDirection(toTarget);
        }
        void Avoid()
        {
            if (!lateralDirectionSet)
            {
                lateralOffset = Vector3D.Reject(Vector3D.Up, originalDirection);
                if (lateralOffset.LengthSquared() < 1e-4)
                {
                    lateralOffset = Vector3D.Reject(Vector3D.Right, originalDirection);
                }
                lateralOffset = Vector3D.Normalize(lateralOffset) * LateralAvoidDistance;
                lateralDirectionSet = true;
            }

            var sideTarget = remote.GetPosition() + lateralOffset;
            var toSide = sideTarget - remote.GetPosition();

            if (IsObstacleAhead(Vector3D.Normalize(toSide)))
            {
                StopAllThrust();
                StopGyros();
                BroadcastStatus("Obstacle while avoiding. Holding...");
                return;
            }

            var toTarget = Vector3D.Normalize(toSide);
            ApplyThrust(toTarget * 10000);

            if (toSide.Length() < 100)
            {
                StopAllThrust();
                StopGyros();
                currentState = NavState.Returning;
                BroadcastStatus("Avoided. Returning to path.");
            }
        }
        void ReturnToPath()
        {
            var fromStart = remote.GetPosition() - startPosition;
            var proj = Vector3D.ProjectOnVector(ref fromStart, ref originalDirection);
            double distFromPath = (fromStart - proj).Length();

            if (distFromPath < 50)
            {
                currentState = NavState.Accelerating;
                lateralDirectionSet = false;
                BroadcastStatus("Back on course.");
            }
            else
            {
                var correction = Vector3D.Normalize(-(fromStart - proj));
                ApplyThrust(correction * 5000);
            }
        }
        void Distress()
        {
            if (beacon != null) beacon.Enabled = true;
            BroadcastStatus($"DISTRESS: Engines damaged!, waiting in position {VecToStr(remote.GetPosition())}");
            StopAllThrust();
        }

        // === UTILIDAD ===

        void ApplyThrust(Vector3D force)
        {
            double mass = remote.CalculateShipMass().PhysicalMass;
            var requiredAccel = force / mass;

            foreach (var t in thrusters)
            {
                var d = t.WorldMatrix.Backward;
                double dot = d.Dot(requiredAccel);
                t.Enabled = true;
                t.ThrustOverridePercentage = dot > 0 ? 1f : 0f;
            }
        }
        void StopAllThrust()
        {
            foreach (var t in thrusters)
            {
                t.Enabled = false;
                t.ThrustOverridePercentage = 0;
            }
        }

        void AlignToDirection(Vector3D direction)
        {
            direction.Normalize();
            var shipMatrix = remote.WorldMatrix;
            var shipForward = shipMatrix.Forward;

            double angleFW = AngleBetweenVectors(shipForward, direction);
            Echo($"Alineación: {angleFW:F2}");
            if (angleFW <= gyrosThr)
            {
                StopGyros();
                Echo("Alineado con el objetivo.");
                return;
            }
            Echo("Alineando...");

            if (angleFW > gyrosThr)
            {
                var rotationAxisFW = Vector3D.Cross(shipForward, direction);
                if (rotationAxisFW.Length() <= 0.001) rotationAxisFW = new Vector3D(0, 1, 0);
                ApplyGyroOverride(rotationAxisFW);
            }
        }
        void ApplyGyroOverride(Vector3D axis)
        {
            foreach (var gyro in gyros)
            {
                var localAxis = Vector3D.TransformNormal(axis, MatrixD.Transpose(gyro.WorldMatrix));
                var gyroRot = localAxis * -gyrosSpeed;
                gyro.GyroOverride = true;
                gyro.Pitch = (float)gyroRot.X;
                gyro.Yaw = (float)gyroRot.Y;
                gyro.Roll = (float)gyroRot.Z;
            }
        }
        void StopGyros()
        {
            foreach (var gyro in gyros)
            {
                gyro.GyroOverride = false;
                gyro.Pitch = 0;
                gyro.Yaw = 0;
                gyro.Roll = 0;
            }
        }

        bool IsObstacleAhead(Vector3D direction)
        {
            if (camera.CanScan(CollisionDetectRange))
            {
                lastHit = camera.Raycast(CollisionDetectRange, direction);
                return !lastHit.IsEmpty();
            }
            return false;
        }
        void BroadcastStatus(string msg)
        {
            IGC.SendBroadcastMessage("NAV_STATUS", msg);
            Echo(msg);
        }

        static double AngleBetweenVectors(Vector3D v1, Vector3D v2)
        {
            v1.Normalize();
            v2.Normalize();
            double dot = Vector3D.Dot(v1, v2);
            dot = MathHelper.Clamp(dot, -1.0, 1.0);
            return Math.Acos(dot);
        }
        static bool VectorAligned(Vector3D a, Vector3D b)
        {
            a.Normalize(); b.Normalize();
            return Vector3D.Dot(a, b) > 0.99;
        }
        static int StrToInt(string s)
        {
            return int.Parse(s);
        }
        static Vector3D StrToVec(string s)
        {
            var nums = s.Split(':');
            if (nums.Length != 3) throw new Exception($"{s} is not a valid vector => x:y:z");
            return new Vector3D(double.Parse(nums[0]), double.Parse(nums[1]), double.Parse(nums[2]));
        }
        static string VecToStr(Vector3D v)
        {
            return $"{v.X:F2}:{v.Y:F2}:{v.Z:F2}";
        }
        T GetBlockWithName<T>(string name) where T : class, IMyTerminalBlock
        {
            List<T> blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid);

            return blocks.FirstOrDefault(b => b.CustomName == name);
        }
        List<T> GetBlocksOfType<T>() where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid);
            return blocks.ToList();
        }
    }
}
