using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace NaveAlign
{
    partial class Program : MyGridProgram
    {
        const string shipProgrammableBlock = "HT Automaton Programmable Block Ship";
        const string shipRemoteControlLocking = "HT Remote Control Locking";
        const string shipConnectorA = "HT Connector A";

        const double gyrosThr = 0.001; //Precisión de alineación
        const double gyrosSpeed = 2f; //Velocidad de los giroscopios

        const double arrivalThr = 0.5; //Precisión de aproximación 0.5 metros
        const double maxApproachSpeed = 25.0; //Velocidad máxima de llegada
        const double maxApproachSpeedAprox = 15.0; //Velocidad máxima de aproximación
        const double maxApproachSpeedLocking = 5.0; //Velocidad máxima en el último waypoint
        const double slowdownDistance = 50.0; //Distancia de frenada

        readonly IMyProgrammableBlock pb;
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
        static double AngleBetweenVectors(Vector3D v1, Vector3D v2)
        {
            v1.Normalize();
            v2.Normalize();
            double dot = Vector3D.Dot(v1, v2);
            dot = MathHelper.Clamp(dot, -1.0, 1.0);
            return Math.Acos(dot);
        }

        public Program()
        {
            pb = GetBlockWithName<IMyProgrammableBlock>(shipProgrammableBlock);
            if (pb == null)
            {
                Echo($"Programmable Block {shipProgrammableBlock} no encontrado.");
                return;
            }

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
            if (parts.Length != 2) return;

            var coords = parts[0].Split('|');
            if (coords.Length != 3) return;
            targetForward = -Vector3D.Normalize(StrToVector(coords[0]));
            targetUp = Vector3D.Normalize(StrToVector(coords[1]));
            waypoints = ParseWaypoints(coords[2]);

            reachCommand = parts[1];

            hasTarget = true;
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
            ResetGyros();
            ResetThrust();
        }

        void NavigateWaypoints()
        {
            if (currentTarget >= waypoints.Count)
            {
                Echo("Última posición alcanzada.");
                Runtime.UpdateFrequency = UpdateFrequency.None;
                pb.TryRun(reachCommand);
                DoStopShip();
                return;
            }

            var currentPos = connectorA.GetPosition();
            var targetPos = waypoints[currentTarget];
            var toTarget = targetPos - currentPos;
            double distance = toTarget.Length();

            Echo($"Progreso: {currentTarget + 1}/{waypoints.Count}. Distancia: {distance:F2}m");
            Echo($"Command en Destino? {!string.IsNullOrWhiteSpace(reachCommand)}");

            if (distance < arrivalThr)
            {
                currentTarget++;
                ResetThrust();
                Echo("Punto alcanzado, pasando al siguiente.");
                return;
            }

            var neededForce = CalculateThrustForce(toTarget, distance);

            ApplyThrust(neededForce);
        }
        Vector3D CalculateThrustForce(Vector3D toTarget, double distance)
        {
            var desiredDirection = Vector3D.Normalize(toTarget);
            var currentVelocity = remoteLocking.GetShipVelocities().LinearVelocity;

            //Calcula velocidad deseada basada en distancia, cuando estemos avanzando hacia el último waypoint.
            double approachSpeed;
            if (currentTarget == 0) approachSpeed = maxApproachSpeed; //Velocidad hasta el primer punto de aproximación.
            else if (currentTarget == waypoints.Count - 1) approachSpeed = maxApproachSpeedLocking; //Velocidad desde el úlimo punto de aproximación.
            else approachSpeed = maxApproachSpeedAprox; //Velocidad entre puntos de aproximación.

            double desiredSpeed = approachSpeed;
            if (distance < slowdownDistance && (currentTarget == 0 || currentTarget == waypoints.Count - 1))
            {
                desiredSpeed = Math.Max(distance / slowdownDistance * approachSpeed, 0.5);
            }

            var desiredVelocity = desiredDirection * desiredSpeed;
            var velocityError = desiredVelocity - currentVelocity;

            double mass = remoteLocking.CalculateShipMass().PhysicalMass;
            return velocityError * mass * 0.5;  // Ganancia ajustable.
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
            double angleUP = AngleBetweenVectors(shipUp, targetUp);
            Echo($"Alineación: {angleFW:F2} | {angleUP:F2}");

            if (angleFW <= gyrosThr && angleUP <= gyrosThr)
            {
                ResetGyros();
                Echo("Alineado con el objetivo.");
                return;
            }
            Echo("Alineando...");

            if (angleFW > gyrosThr)
            {
                var rotationAxisFW = Vector3D.Cross(shipForward, targetForward);
                if (rotationAxisFW.Length() <= 0.001) rotationAxisFW = new Vector3D(0, 1, 0);
                ApplyGyroOverride(rotationAxisFW);
            }

            if (angleUP > gyrosThr)
            {
                var rotationAxisUP = Vector3D.Cross(shipUp, targetUp);
                if (rotationAxisUP.Length() <= 0.001) rotationAxisUP = new Vector3D(1, 0, 0);
                ApplyGyroOverride(rotationAxisUP);
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
    }
}
