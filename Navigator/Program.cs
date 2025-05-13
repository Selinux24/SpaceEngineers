using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string NavTag = "NAV|";
        const string RemoteName = "Stingray Remote Control";
        const string CameraName = "Stingray Camera";
        const string BeaconName = "Stingray Distress Beacon";

        //Velocidad de los giroscopios
        const double GyrosSpeed = 5f;

        // Velocidad máxima de crucero
        const double MaxSpeed = 10.0;
        const double MaxSpeedTrh = 0.95;

        // Tiempo de encendido de thrusters hasta alineación
        const double ThrustSeconds = 5.0;
        // Precisión de alineación
        const double AlignThr = 0.001;
        // Precisión de alineación
        const double CruiseAlignThr = 0.01;

        // Rango de frenado hasta el objetivo
        const double ToTargetBrakingDistance = 1000.0;

        // Rango de detección de colisiones
        const double CollisionDetectRange = 1000.0;
        const double EvadingWaypointDistance = 100.0;

        /*
        Fase1 - Encarar al destino
        Fase2 - Acelerar hasta velocidad de crucero
        Fase3 - Mantener velocidad de crucero con thrusters apagados
        Fase4 - Frenado al llegar al destino
        Fase5 - Aviso de llegada

        Con la detección de un obstáculo, se activa el modo de esquivar.
        Fase1 - Localizar el obstáculo y obtener información de radio y distancia al obstáculo
        Fase2 - Frenar
        Fase3 - Calcular tres puntos de ruta de esquiva
            Punto A: Desde el punto actual de la nave, con el vector UP de la nave multiplicado por el radio del obstáculo + margen de seguridad
            Punto B: Desde el punto A, con el vector FORWARD de la nave multiplicado por la distancia al obstáculo * 2
            Punto C: Desde el punto B, con el vector DOWN de la nave multiplicado por el radio del obstáculo + margen de seguridad
        Fase4 - Desplazarse siguiendo los puntos sin girar
        Fase5 - Aceleración
         */

        enum NavState
        {
            Idle, Locating, Accelerating, Braking, Cruising, Avoiding, Returning, Distress
        }

        readonly IMyRemoteControl remote;
        readonly IMyCameraBlock camera;
        readonly List<IMyThrust> thrusters;
        readonly List<IMyGyro> gyros;
        readonly IMyBeacon beacon;

        NavState currentState = NavState.Idle;
        Vector3D destination;
        bool hasTarget = false;
        readonly List<Vector3D> evadingPoints = new List<Vector3D>();

        MyDetectedEntityInfo lastHit;
        Vector3D toTarget;
        Vector3D toTargetN;

        bool thrusting = false;
        DateTime alignThrustStart = DateTime.Now;

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
                hasTarget = StrToInt(parts[5]) == 1;
            }
        }
        void SaveState()
        {
            var parts = new List<string>
            {
                $"{(int)currentState}",
                $"{VecToStr(destination)}",
                $"{(hasTarget?1:0)}",
            };
            Storage = string.Join("|", parts);
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (argument.StartsWith(NavTag))
            {
                StartNavigation(argument.Substring(NavTag.Length));
                return;
            }

            Echo($"State {currentState}");

            if (!hasTarget || currentState == NavState.Idle)
            {
                IsObstacleAhead();
                Echo("Waiting for position...");
                return;
            }

            toTarget = destination - camera.GetPosition();
            toTargetN = Vector3D.Normalize(toTarget);

            Echo($"To target: {toTarget.Length():F2}m.");
            Echo($"T: {VecToStr(toTargetN)}");
            Echo($"S: {VecToStr(remote.WorldMatrix.Forward)}");

            Echo($"HIT {VecToStr(lastHit.HitPosition ?? Vector3D.Zero)}");
            if (!lastHit.IsEmpty())
            {
                Echo($"{lastHit.Name} - Type {lastHit.Type}");
                Echo($"{VecToStr(lastHit.BoundingBox.Extents)}");
            }

            switch (currentState)
            {
                case NavState.Distress:
                    Distress();
                    break;

                case NavState.Locating:
                    Locate();
                    break;
                case NavState.Accelerating:
                    Accelerate();
                    break;
                case NavState.Cruising:
                    Cruise();
                    break;
                case NavState.Braking:
                    Brake();
                    break;

                case NavState.Avoiding:
                    Avoid();
                    break;
            }

            SaveState();
        }

        void StartNavigation(string navParams)
        {
            destination = StrToVec(navParams);
            hasTarget = true;
            evadingPoints.Clear();
            currentState = NavState.Locating;
            BroadcastStatus("Starting navigation.");
            SaveState();
        }

        // === ESTADOS ===

        void Distress()
        {
            if (beacon != null) beacon.Enabled = true;
            BroadcastStatus($"DISTRESS: Engines damaged!, waiting in position {VecToStr(remote.GetPosition())}");
            ResetThrust();
            ResetGyros();
        }

        void Locate()
        {
            if (AlignToDirection(remote.WorldMatrix.Forward, AlignThr))
            {
                BroadcastStatus("Destination located. Initializing acceleration.");
                currentState = NavState.Accelerating;
            }
        }
        void Accelerate()
        {
            if (IsObstacleAhead())
            {
                BroadcastStatus("Obstacle detected. Avoiding...");
                currentState = NavState.Avoiding;
                return;
            }

            var shipVelocity = remote.GetShipVelocities().LinearVelocity.Length();
            if (shipVelocity >= MaxSpeed * MaxSpeedTrh)
            {
                BroadcastStatus("Reached cruise speed. Deactivating thrusters.");
                currentState = NavState.Cruising;
                return;
            }

            // Acelerar
            ThrustToTarget();
        }
        void Cruise()
        {
            if (IsObstacleAhead())
            {
                BroadcastStatus("Obstacle detected. Avoiding...");
                currentState = NavState.Avoiding;

                return;
            }

            if (toTarget.Length() < ToTargetBrakingDistance)
            {
                BroadcastStatus("Destination reached. Braking.");
                currentState = NavState.Braking;

                return;
            }

            // Mantener velocidad
            if (!AlignToDirection(remote.WorldMatrix.Forward, CruiseAlignThr))
            {
                // Encender los propulsores hasta alinear de nuevo el vector velocidad con el vector hasta el objetivo
                ThrustToTarget();
                alignThrustStart = DateTime.Now;
                thrusting = true;

                return;
            }

            if (thrusting)
            {
                // Propulsores encendidos para recuperar la alineación
                if ((DateTime.Now - alignThrustStart).TotalSeconds > ThrustSeconds)
                {
                    // Tiempo de alineación consumido. Desactivar propulsores
                    EnterCruising();
                    thrusting = false;
                }

                return;
            }

            var shipVelocity = remote.GetShipVelocities().LinearVelocity.Length();
            if (shipVelocity > MaxSpeed)
            {
                // Velocidad máxima superada. Encender propulsores en neutro para frenar
                ResetThrust();
                ResetGyros();

                return;
            }

            if (shipVelocity < MaxSpeed * MaxSpeedTrh)
            {
                // Por debajo de la velocidad deseada. Acelerar hasta alcanzarla
                ThrustToTarget();

                return;
            }

            EnterCruising();
        }
        void Brake()
        {
            ResetThrust();
            ResetGyros();

            var shipVelocity = remote.GetShipVelocities().LinearVelocity.Length();
            if (shipVelocity <= 0.1)
            {
                BroadcastStatus("Destination reached.");
                currentState = NavState.Idle;
            }
        }

        void Avoid()
        {
            // Calcular los puntos de evasión
            if (!CalculateEvadingWaypoints())
            {
                //No se puede calcular un punto de evasión
                currentState = NavState.Braking;

                return;
            }

            // Navegar entre los puntos de evasión
            if (evadingPoints.Count > 0)
            {
                NavigateTo(evadingPoints[0]);

                return;
            }

            ResetThrust();

            // Volver a navegación cuando se alcance el último punto de navegación
            currentState = NavState.Locating;
        }
        bool CalculateEvadingWaypoints()
        {
            if (lastHit.IsEmpty())
            {
                return false;
            }

            var boxCenter = lastHit.BoundingBox.Center;
            var boxPerimeter = lastHit.BoundingBox.Perimeter;

            //Punto sobre el obstáculo desde el punto de vista de la nave
            var p1 = boxCenter + (remote.WorldMatrix.Up * boxPerimeter);
            evadingPoints.Add(p1);

            //Punto al otro lado del obstáculo desde el punto de vista de la nave
            var p2 = boxCenter + (remote.WorldMatrix.Forward * CollisionDetectRange);
            evadingPoints.Add(p2);

            return true;
        }
        void NavigateTo(Vector3D wayPoint)
        {
            if (Vector3D.Distance(wayPoint, camera.GetPosition()) <= EvadingWaypointDistance)
            {
                // Waypoint alcanzado
                evadingPoints.RemoveAt(0);

                return;
            }

            ThrustToPosition(wayPoint);
        }

        // === UTILIDAD ===

        void BroadcastStatus(string msg)
        {
            IGC.SendBroadcastMessage("NAV_STATUS", msg);
            Echo(msg);
        }

        void ThrustToTarget()
        {
            var force = CalculateThrustForce(toTargetN, MaxSpeed);
            ApplyThrust(force);
        }
        void ThrustToPosition(Vector3D position)
        {
            var v = Vector3D.Normalize(camera.GetPosition() - position);

            var force = CalculateThrustForce(v, MaxSpeed);
            ApplyThrust(force);
        }

        Vector3D CalculateThrustForce(Vector3D toTarget, double desiredSpeed)
        {
            var desiredDirection = Vector3D.Normalize(toTarget);
            var currentVelocity = remote.GetShipVelocities().LinearVelocity;

            var desiredVelocity = desiredDirection * desiredSpeed;
            var velocityError = desiredVelocity - currentVelocity;

            double mass = remote.CalculateShipMass().PhysicalMass;
            return velocityError * mass * 0.5;  // Ganancia ajustable.
        }
        void ApplyThrust(Vector3D force)
        {
            foreach (var t in thrusters)
            {
                var dir = t.WorldMatrix.Backward;
                double alignment = dir.Dot(force);

                t.Enabled = true;
                t.ThrustOverridePercentage = alignment > 0 ? (float)Math.Min(alignment / t.MaxEffectiveThrust, 1f) : 0f;
            }
        }
        void ResetThrust()
        {
            foreach (var t in thrusters)
            {
                t.Enabled = true;
                t.ThrustOverridePercentage = 0;
            }
        }
        void StopThrust()
        {
            foreach (var t in thrusters)
            {
                t.Enabled = false;
            }
        }

        bool AlignToDirection(Vector3D direction, double thr)
        {
            double angle = AngleBetweenVectors(direction, toTargetN);
            Echo($"Alineación: {angle:F4}");

            var rotationAxis = Vector3D.Cross(direction, toTargetN);
            if (IsZero(rotationAxis, thr))
            {
                ResetGyros();
                Echo("Alineado con el objetivo.");
                return true;
            }

            Echo("Alineando...");
            ApplyGyroOverride(rotationAxis);

            return false;
        }
        static bool IsZero(Vector3D v, double thr)
        {
            return Math.Abs(v.X) < thr && Math.Abs(v.Y) < thr && Math.Abs(v.Z) < thr;
        }
        void ApplyGyroOverride(Vector3D axis)
        {
            foreach (var gyro in gyros)
            {
                var localAxis = Vector3D.TransformNormal(axis, MatrixD.Transpose(gyro.WorldMatrix));
                var gyroRot = localAxis * -GyrosSpeed;
                gyro.GyroOverride = true;
                gyro.Pitch = (float)gyroRot.X;
                gyro.Yaw = (float)gyroRot.Y;
                gyro.Roll = (float)gyroRot.Z;
            }
        }
        void ResetGyros()
        {
            foreach (var gyro in gyros)
            {
                gyro.GyroOverride = false;
                gyro.Pitch = 0;
                gyro.Yaw = 0;
                gyro.Roll = 0;
            }
        }

        void EnterCruising()
        {
            ResetThrust();
            StopThrust();
            ResetGyros();
        }

        bool IsObstacleAhead()
        {
            camera.EnableRaycast = true;
            if (camera.CanScan(CollisionDetectRange))
            {
                lastHit = camera.Raycast(CollisionDetectRange);
                return !lastHit.IsEmpty() && Vector3D.Distance(lastHit.HitPosition, camera.GetPosition()) <= CollisionDetectRange;
            }

            return false;
        }

        static double AngleBetweenVectors(Vector3D v1, Vector3D v2)
        {
            v1.Normalize();
            v2.Normalize();
            double dot = Vector3D.Dot(v1, v2);
            dot = MathHelper.Clamp(dot, -1.0, 1.0);
            return Math.Acos(dot);
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
