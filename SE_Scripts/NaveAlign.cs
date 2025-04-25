using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace NaveAlign
{
    partial class Program : MyGridProgram
    {
        const string channel = "SHIPS_DELIVERY";
        const string shipRemoteControlLocking = "HT Remote Control Locking";
        const string shipConnectorA = "HT Connector A";
        const float thr = 2f;
        const double angleThr = 0.001;
        const double ArrivalDistance = 0.5;    // Precisión: 0.5 metros.
        const double MaxApproachSpeed = 25.0;   // Velocidad máxima de llegada.
        const double MaxApproachSpeedAprox = 15.0;   // Velocidad máxima de aproximación.
        const double MaxApproachSpeedLocking = 5.0;   // Velocidad máxima en el último waypoint.
        const double SlowdownDistance = 50.0;  // Distancia de frenada.

        readonly IMyRemoteControl remoteLocking;
        readonly IMyShipConnector connectorA;
        readonly List<IMyThrust> thrusters = new List<IMyThrust>();
        readonly List<IMyGyro> gyros = new List<IMyGyro>();

        List<Vector3D> waypoints = new List<Vector3D>();
        int currentTarget = 0;
        Vector3D targetForward = new Vector3D(1, 0, 0);
        Vector3D targetUp = new Vector3D(0, 1, 0);
        bool hasTarget = false;
        string reachCommand = null;
        string reachTimer = null;

        T GetBlockWithName<T>(string name) where T : class, IMyTerminalBlock
        {
            List<T> blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid);

            return blocks.FirstOrDefault(b => b.CustomName.Contains(name));
        }
        static Vector3D StrToVector(string input)
        {
            var trimmed = input.Split(':');
            return new Vector3D(
                double.Parse(trimmed[0]),
                double.Parse(trimmed[1]),
                double.Parse(trimmed[2])
            );
        }
        static string VectorToStr(Vector3D vector)
        {
            return $"({vector.X:F2}, {vector.Y:F2}, {vector.Z:F2})";
        }
        static double AngleBetweenVectors(Vector3D v1, Vector3D v2)
        {
            v1.Normalize();
            v2.Normalize();
            double dot = Vector3D.Dot(v1, v2);
            dot = MathHelper.Clamp(dot, -1.0, 1.0);
            return Math.Acos(dot);
        }
        void PrintGPS(string name, Vector3D v, string color)
        {
            Echo($"GPS:{name}:{v.X:F2}:{v.Y:F2}:{v.Z:F2}:{color}:");
        }
        void SendIGCMessage(string message)
        {
            IGC.SendBroadcastMessage(channel, message);
        }

        public Program()
        {
            remoteLocking = GetBlockWithName<IMyRemoteControl>(shipRemoteControlLocking);
            if (remoteLocking == null)
            {
                Echo($"Control remoto de atraque '{shipRemoteControlLocking}' no locallizado.");
                return;
            }

            connectorA = GetBlockWithName<IMyShipConnector>(shipConnectorA);
            if (connectorA == null)
            {
                Echo($"Connector de atraque A '{shipConnectorA}' no locallizado.");
                return;
            }

            GridTerminalSystem.GetBlocksOfType(gyros, g => g.CubeGrid == Me.CubeGrid);
            GridTerminalSystem.GetBlocksOfType(thrusters, t => t.CubeGrid == Me.CubeGrid);

            Runtime.UpdateFrequency = UpdateFrequency.None;
            Echo("Working");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (!string.IsNullOrEmpty(argument))
            {
                if (argument.StartsWith("STOP"))
                {
                    DoStopShip();
                    return;
                }

                InitAlignShip(argument);
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
                return;
            }

            if ((updateSource & UpdateType.Update1) != 0)
            {
                DoAlignShip();
            }
        }

        void InitAlignShip(string data)
        {
            currentTarget = 0;
            waypoints.Clear();
            hasTarget = false;

            var parts = data.Split('¬');
            if (parts.Length == 0) return;

            var coords = parts[0].Split('|');
            if (coords.Length != 3) return;
            targetForward = -Vector3D.Normalize(StrToVector(coords[0]));
            targetUp = Vector3D.Normalize(StrToVector(coords[1]));
            waypoints = ParseWaypoints(coords[2]);
            hasTarget = true;

            if (parts.Length >= 2) ParseAction(parts[1]);

            if (parts.Length >= 3) ParseAction(parts[2]);
        }
        List<Vector3D> ParseWaypoints(string data)
        {
            string[] points = data.Split(';');
            List<Vector3D> wp = new List<Vector3D>();
            for (int i = 0; i < points.Length; i++)
            {
                Vector3D waypoint = StrToVector(points[i]);
                PrintGPS($"WP_{i}", waypoint, "#FF00FFFF");
                wp.Add(waypoint);
            }
            return wp;
        }
        void ParseAction(string action)
        {
            if (action.StartsWith("Command="))
            {
                reachCommand = action;
            }
            else
            {
                reachTimer = action;
            }
        }

        void DoAlignShip()
        {
            if (!hasTarget)
            {
                Echo("Esperando vectores...");
                return;
            }

            Echo($"{currentTarget}");
            Echo($"{waypoints.Count}");

            AlignToVectors(targetForward, targetUp);
            NavigateWaypoints();
        }
        void DoStopShip()
        {
            currentTarget = 0;
            waypoints.Clear();
            hasTarget = false;
            reachCommand = null;
            reachTimer = null;
            ResetGyros();
            ResetThrust();
        }

        void NavigateWaypoints()
        {
            if (currentTarget >= waypoints.Count)
            {
                Runtime.UpdateFrequency = UpdateFrequency.None;
                DoStopShip();
                DoReachActions();
                return;
            }
            Vector3D currentPos = connectorA.GetPosition();
            Vector3D targetPos = waypoints[currentTarget];
            Vector3D toTarget = targetPos - currentPos;
            double distance = toTarget.Length();

            Echo($"Destino: {currentTarget + 1}/{waypoints.Count}\nDistancia: {distance:F2} m");
            Echo($"Current={VectorToStr(currentPos)}");
            Echo($"Target={VectorToStr(targetPos)}");

            if (distance < ArrivalDistance)
            {
                currentTarget++;
                ResetThrust();
                Echo("Llegó al punto, pasando al siguiente.");
                return;
            }

            Vector3D desiredDirection = toTarget;
            desiredDirection.Normalize();

            Vector3D currentVelocity = remoteLocking.GetShipVelocities().LinearVelocity;

            // Calcula velocidad deseada basada en distancia, cuando estemos avanzando hacia el último waypoint.
            double approachSpeed;
            if (currentTarget == 0)
            {
                // Velocidad hasta el primer punto de aproximación.
                approachSpeed = MaxApproachSpeed;
            }
            else if (currentTarget == waypoints.Count - 1)
            {
                // Velocidad desde el úlimo punto de aproximación.
                approachSpeed = MaxApproachSpeedLocking;
            }
            else
            {
                //Velocidad entre puntos de aproximación.
                approachSpeed = MaxApproachSpeedAprox;
            }

            double desiredSpeed = approachSpeed;
            if (distance < SlowdownDistance && (currentTarget == 0 || currentTarget == waypoints.Count - 1))
            {
                desiredSpeed = Math.Max(distance / SlowdownDistance * approachSpeed, 0.5);
            }

            Vector3D desiredVelocity = desiredDirection * desiredSpeed;
            Vector3D velocityError = desiredVelocity - currentVelocity;

            double mass = remoteLocking.CalculateShipMass().PhysicalMass;
            Vector3D neededForce = velocityError * mass * 0.5;  // Ganancia ajustable.

            ApplyThrust(neededForce);
        }
        void ApplyThrust(Vector3D force)
        {
            foreach (var thruster in thrusters)
            {
                Vector3D thrustDir = thruster.WorldMatrix.Backward;
                double alignment = thrustDir.Dot(force);

                thruster.ThrustOverridePercentage = alignment > 0 ? (float)Math.Min(alignment / thruster.MaxEffectiveThrust, 1f) : 0f;
            }
        }
        void ResetThrust()
        {
            foreach (var thruster in thrusters)
            {
                thruster.ThrustOverridePercentage = 0f;
            }
        }

        void AlignToVectors(Vector3D targetForward, Vector3D targetUp)
        {
            MatrixD shipMatrix = remoteLocking.WorldMatrix;
            Vector3D shipForward = shipMatrix.Forward;
            Vector3D shipUp = shipMatrix.Up;

            double angleFW = AngleBetweenVectors(shipForward, targetForward);
            Echo($"TargetFWD {VectorToStr(targetForward)}");
            Echo($"ShipFWD {VectorToStr(shipForward)}");
            Echo($"AngleFW {angleFW:F2}");

            double angleUP = AngleBetweenVectors(shipUp, targetUp);
            Echo($"TargetUP {VectorToStr(targetUp)}");
            Echo($"ShipUP {VectorToStr(shipUp)}");
            Echo($"AngleUP {angleUP:F2}");

            if (angleFW <= angleThr && angleUP <= angleThr)
            {
                ResetGyros();
                Echo("Alineado con la base.");
                return;
            }

            if (angleFW > angleThr)
            {
                Vector3D rotationAxisFW = Vector3D.Cross(shipForward, targetForward);
                Echo($"RotAxix {VectorToStr(rotationAxisFW)} {rotationAxisFW.Length()}");
                if (rotationAxisFW.Length() <= 0.001) rotationAxisFW = new Vector3D(0, 1, 0);
                ApplyGyroOverride(rotationAxisFW);
                Echo("Applyed FW");
            }

            if (angleUP > angleThr)
            {
                Vector3D rotationAxisUP = Vector3D.Cross(shipUp, targetUp);
                Echo($"RotAxix {VectorToStr(rotationAxisUP)} {rotationAxisUP.Length()}");
                if (rotationAxisUP.Length() <= 0.001) rotationAxisUP = new Vector3D(1, 0, 0);
                ApplyGyroOverride(rotationAxisUP);
                Echo("Applyed UP");
            }

            Echo(DateTime.Now.ToString());
        }
        void ApplyGyroOverride(Vector3D axis)
        {
            foreach (var gyro in gyros)
            {
                var localAxis = Vector3D.TransformNormal(axis, MatrixD.Transpose(gyro.WorldMatrix));
                gyro.GyroOverride = true;
                gyro.Pitch = (float)localAxis.X * -thr;
                gyro.Yaw = (float)localAxis.Y * -thr;
                gyro.Roll = (float)localAxis.Z * -thr;
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

        void DoReachActions()
        {
            if (!string.IsNullOrWhiteSpace(reachTimer))
            {
                var timer = GetBlockWithName<IMyTimerBlock>(reachTimer);
                timer?.ApplyAction("Start");
            }

            if (!string.IsNullOrWhiteSpace(reachCommand))
            {
                SendIGCMessage(reachCommand);
            }
        }
    }
}
