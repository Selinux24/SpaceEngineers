using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        #region Constants
        const string shipProgrammableBlock = "HT Automaton Programmable Block Ship";
        const string shipRemoteControlPilot = "HT Remote Control Pilot";
        const string shipRemoteControlLocking = "HT Remote Control Locking";
        const string shipConnectorA = "HT Connector A";

        const double gyrosThr = 0.001; //Precisión de alineación
        const double gyrosSpeed = 2f; //Velocidad de los giroscopios

        const double arrivalThr = 0.5; //Precisión de aproximación 0.5 metros
        const double maxApproachSpeed = 25.0; //Velocidad máxima de llegada
        const double maxApproachSpeedAprox = 15.0; //Velocidad máxima de aproximación
        const double maxApproachSpeedLocking = 5.0; //Velocidad máxima en el último waypoint
        const double slowdownDistance = 50.0; //Distancia de frenada

        const double arrivalThreshold = 200.0;
        #endregion

        #region Blocks
        readonly IMyProgrammableBlock pb;
        readonly IMyRemoteControl remotePilot;
        readonly IMyRemoteControl remoteLocking;
        readonly IMyShipConnector connectorA;
        readonly List<IMyThrust> thrusters = new List<IMyThrust>();
        readonly List<IMyGyro> gyros = new List<IMyGyro>();
        #endregion

        enum Tasks { NONE, ALIGN, ARRIVAL };

        readonly AlignData alignData = new AlignData();
        readonly ArrivalData arrivalData = new ArrivalData();
        Tasks currentTask = Tasks.NONE;

        T GetBlockWithName<T>(string name) where T : class, IMyTerminalBlock
        {
            List<T> blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid);

            return blocks.FirstOrDefault(b => b.CustomName.Contains(name));
        }

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;

            LoadFromStorage();

            pb = GetBlockWithName<IMyProgrammableBlock>(shipProgrammableBlock);
            if (pb == null)
            {
                Echo($"Programmable Block {shipProgrammableBlock} not found.");
                return;
            }

            remotePilot = GetBlockWithName<IMyRemoteControl>(shipRemoteControlPilot);
            if (remotePilot == null)
            {
                Echo($"Remote Control {shipRemoteControlPilot} not found.");
                return;
            }

            remoteLocking = GetBlockWithName<IMyRemoteControl>(shipRemoteControlLocking);
            if (remoteLocking == null)
            {
                Echo($"Remote Control '{shipRemoteControlLocking}' not found.");
                return;
            }

            connectorA = GetBlockWithName<IMyShipConnector>(shipConnectorA);
            if (connectorA == null)
            {
                Echo($"Connector '{shipConnectorA}' not found.");
                return;
            }

            GridTerminalSystem.GetBlocksOfType(gyros, g => g.CubeGrid == Me.CubeGrid);
            if (gyros.Count == 0)
            {
                Echo("Grid without gyroscopes.");
                return;
            }

            GridTerminalSystem.GetBlocksOfType(thrusters, t => t.CubeGrid == Me.CubeGrid);
            if (thrusters.Count == 0)
            {
                Echo("Grid without thrusters.");
                return;
            }

            Echo("Working!");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (!string.IsNullOrEmpty(argument))
            {
                if (argument.StartsWith("STOP"))
                {
                    DoStop();
                    return;
                }
                if (argument.StartsWith("ALIGN"))
                {
                    InitializeAlign(Utils.ReadArgument(argument, "ALIGN"));
                    return;
                }
                if (argument.StartsWith("ARRIVAL"))
                {
                    InitializeArrival(Utils.ReadArgument(argument, "ARRIVAL"));
                    return;
                }

                Echo($"Unknown argument: {argument}");
                return;
            }

            DoArrival();
            DoAlign();
        }

        void DoStop()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None; // Detener comprobaciones
            currentTask = Tasks.NONE;
            alignData.Clear();
            arrivalData.Clear();
            SaveToStorage();
            ResetGyros();
            ResetThrust();
            Echo("Stopped.");
        }

        void InitializeAlign(string message)
        {
            alignData.Initialize(message);
            currentTask = Tasks.ALIGN;
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            SaveToStorage();
        }
        void DoAlign()
        {
            if (currentTask != Tasks.ALIGN)
            {
                return;
            }

            if (!alignData.HasTarget)
            {
                Echo("Align target undefined...");
                return;
            }

            NavigateWaypoints();
        }
        void NavigateWaypoints()
        {
            if (alignData.CurrentTarget >= alignData.Waypoints.Count)
            {
                Echo("Destination reached.");
                ExcuteAction(alignData.Command);
                DoStop();
                return;
            }

            AlignToVectors(alignData.TargetForward, alignData.TargetUp);

            var currentPos = connectorA.GetPosition();
            var targetPos = alignData.Waypoints[alignData.CurrentTarget];
            var toTarget = targetPos - currentPos;
            double distance = toTarget.Length();

            Echo($"Distance to destination: {distance:F2}m");
            Echo($"Progress: {alignData.CurrentTarget + 1}/{alignData.Waypoints.Count}.");
            Echo($"Has command? {!string.IsNullOrWhiteSpace(alignData.Command)}");

            if (distance < arrivalThr)
            {
                alignData.Next();
                SaveToStorage();
                ResetThrust();
                Echo("Waypoint reached. Moving to the next.");
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
            if (alignData.CurrentTarget == 0) approachSpeed = maxApproachSpeed; //Velocidad hasta el primer punto de aproximación.
            else if (alignData.CurrentTarget == alignData.Waypoints.Count - 1) approachSpeed = maxApproachSpeedLocking; //Velocidad desde el úlimo punto de aproximación.
            else approachSpeed = maxApproachSpeedAprox; //Velocidad entre puntos de aproximación.

            double desiredSpeed = approachSpeed;
            if (distance < slowdownDistance && (alignData.CurrentTarget == 0 || alignData.CurrentTarget == alignData.Waypoints.Count - 1))
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

            double angleFW = Utils.AngleBetweenVectors(shipForward, targetForward);
            double angleUP = Utils.AngleBetweenVectors(shipUp, targetUp);
            Echo($"Target angles: {angleFW:F2} | {angleUP:F2}");

            if (angleFW <= gyrosThr && angleUP <= gyrosThr)
            {
                ResetGyros();
                Echo("Aligned.");
                return;
            }
            Echo("Aligning...");

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

        void InitializeArrival(string message)
        {
            arrivalData.Initialize(message);
            currentTask = Tasks.ARRIVAL;
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            SaveToStorage();
        }
        void DoArrival()
        {
            if (currentTask != Tasks.ARRIVAL)
            {
                return;
            }

            if (!arrivalData.HasPosition)
            {
                Echo("Arrival position undefined...");
                return;
            }

            MonitorizeArrival();
        }
        void MonitorizeArrival()
        {
            double distance = Vector3D.Distance(remotePilot.GetPosition(), arrivalData.TargetPosition);
            if (distance <= arrivalThreshold)
            {
                Echo("Detination reached.");
                ExcuteAction(arrivalData.Command);
                DoStop();

                return;
            }

            Echo($"Distance to destination: {distance:F2}m.");
            Echo($"Has command? {!string.IsNullOrWhiteSpace(arrivalData.Command)}");
        }

        void ExcuteAction(string action)
        {
            if (!string.IsNullOrWhiteSpace(action))
            {
                pb.TryRun(action);
                Echo($"Executing {action}");
            }
        }

        void LoadFromStorage()
        {
            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0)
            {
                return;
            }

            Runtime.UpdateFrequency = (UpdateFrequency)Utils.ReadInt(storageLines, "UpdateFrequency");
            alignData.LoadFromStorage(Utils.ReadString(storageLines, "AlignData"));
            arrivalData.LoadFromStorage(Utils.ReadString(storageLines, "ArrivalData"));
            currentTask = (Tasks)Utils.ReadInt(storageLines, "CurrentTask");
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"UpdateFrequency={(int)Runtime.UpdateFrequency}",
                $"AlignData={alignData.SaveToStorage()}",
                $"ArrivalData={arrivalData.SaveToStorage()}",
                $"CurrentTask={(int)currentTask}",
            };

            Storage = string.Join(Environment.NewLine, parts);
        }
    }
}
