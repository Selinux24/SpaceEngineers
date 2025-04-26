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

            if (parts.Length > 1) ParseAction(parts[1]);

            if (parts.Length > 2) ParseAction(parts[2]);
        }
        static List<Vector3D> ParseWaypoints(string data)
        {
            List<Vector3D> wp = new List<Vector3D>();

            string[] points = data.Split(';');
            for (int i = 0; i < points.Length; i++)
            {
                wp.Add(StrToVector(points[i]));
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
                Echo("Esperando instrucciones...");
                return;
            }

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
                Echo("Última posición alcanzada.");
                Runtime.UpdateFrequency = UpdateFrequency.None;
                DoReachActions();
                DoStopShip();
                return;
            }
            var currentPos = connectorA.GetPosition();
            var targetPos = waypoints[currentTarget];
            var toTarget = targetPos - currentPos;
            double distance = toTarget.Length();

            Echo($"Progreso: {currentTarget + 1}/{waypoints.Count}\nDistancia: {distance:F2} m");
            Echo($"Actual={VectorToStr(currentPos)}");
            Echo($"Destino={VectorToStr(targetPos)}");
            Echo($"Timer en Destino? {!string.IsNullOrWhiteSpace(reachTimer)}");
            Echo($"Command en Destino? {!string.IsNullOrWhiteSpace(reachCommand)}");

            if (distance < ArrivalDistance)
            {
                currentTarget++;
                ResetThrust();
                Echo("Punto alcanzado, pasando al siguiente.");
                return;
            }

            var desiredDirection = Vector3D.Normalize(toTarget);
            var currentVelocity = remoteLocking.GetShipVelocities().LinearVelocity;

            //Calcula velocidad deseada basada en distancia, cuando estemos avanzando hacia el último waypoint.
            double approachSpeed;
            if (currentTarget == 0) approachSpeed = MaxApproachSpeed; //Velocidad hasta el primer punto de aproximación.
            else if (currentTarget == waypoints.Count - 1) approachSpeed = MaxApproachSpeedLocking; //Velocidad desde el úlimo punto de aproximación.
            else approachSpeed = MaxApproachSpeedAprox; //Velocidad entre puntos de aproximación.

            double desiredSpeed = approachSpeed;
            if (distance < SlowdownDistance && (currentTarget == 0 || currentTarget == waypoints.Count - 1))
            {
                desiredSpeed = Math.Max(distance / SlowdownDistance * approachSpeed, 0.5);
            }

            var desiredVelocity = desiredDirection * desiredSpeed;
            var velocityError = desiredVelocity - currentVelocity;

            double mass = remoteLocking.CalculateShipMass().PhysicalMass;
            var neededForce = velocityError * mass * 0.5;  // Ganancia ajustable.

            ApplyThrust(neededForce);
        }
        void ApplyThrust(Vector3D force)
        {
            foreach (var thruster in thrusters)
            {
                var thrustDir = thruster.WorldMatrix.Backward;
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
            var shipMatrix = remoteLocking.WorldMatrix;
            var shipForward = shipMatrix.Forward;
            var shipUp = shipMatrix.Up;

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
                var rotationAxisFW = Vector3D.Cross(shipForward, targetForward);
                Echo($"RotAxix {VectorToStr(rotationAxisFW)} {rotationAxisFW.Length()}");
                if (rotationAxisFW.Length() <= 0.001) rotationAxisFW = new Vector3D(0, 1, 0);
                ApplyGyroOverride(rotationAxisFW);
                Echo("Applyed FW");
            }

            if (angleUP > angleThr)
            {
                var rotationAxisUP = Vector3D.Cross(shipUp, targetUp);
                Echo($"RotAxix {VectorToStr(rotationAxisUP)} {rotationAxisUP.Length()}");
                if (rotationAxisUP.Length() <= 0.001) rotationAxisUP = new Vector3D(1, 0, 0);
                ApplyGyroOverride(rotationAxisUP);
                Echo("Applyed UP");
            }
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
            if (!string.IsNullOrWhiteSpace(reachCommand))
            {
                Echo(reachCommand);
                SendIGCMessage(reachCommand);
            }

            if (!string.IsNullOrWhiteSpace(reachTimer))
            {
                Echo($"Timer: {reachTimer}");
                var timer = GetBlockWithName<IMyTimerBlock>(reachTimer);
                timer?.ApplyAction("Start");
            }
        }
    }
}
