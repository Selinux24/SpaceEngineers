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
        const string shipRemoteControlArrival = "HT Remote Control Pilot";
        const string shipRemoteControlAlign = "HT Remote Control Locking";
        const string shipConnectorA = "HT Connector A";

        const double gyrosThr = 0.001; //Precisión de alineación
        const double gyrosSpeed = 2f; //Velocidad de los giroscopios

        const double arrivalThr = 0.5; //Precisión de aproximación 0.5 metros
        const double arrivalThreshold = 200.0;
        #endregion

        #region Blocks
        readonly IMyProgrammableBlock pb;
        readonly IMyRemoteControl remoteArrival;
        readonly IMyRemoteControl remoteAlign;
        readonly IMyShipConnector connectorA;
        readonly List<IMyThrust> thrusters = new List<IMyThrust>();
        readonly List<IMyGyro> gyros = new List<IMyGyro>();
        #endregion

        readonly AlignData alignData = new AlignData();
        readonly ArrivalData arrivalData = new ArrivalData();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;

            pb = GetBlockWithName<IMyProgrammableBlock>(shipProgrammableBlock);
            if (pb == null)
            {
                Echo($"Programmable Block {shipProgrammableBlock} not found.");
                return;
            }

            remoteArrival = GetBlockWithName<IMyRemoteControl>(shipRemoteControlArrival);
            if (remoteArrival == null)
            {
                Echo($"Remote Control {shipRemoteControlArrival} not found.");
                return;
            }

            remoteAlign = GetBlockWithName<IMyRemoteControl>(shipRemoteControlAlign);
            if (remoteAlign == null)
            {
                Echo($"Remote Control '{shipRemoteControlAlign}' not found.");
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

            LoadFromStorage();

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
                    InitializeAlign(Utils.ReadArgument(argument, "ALIGN", '|'));
                    return;
                }
                if (argument.StartsWith("ARRIVAL"))
                {
                    InitializeArrival(Utils.ReadArgument(argument, "ARRIVAL", '|'));
                    return;
                }

                Echo($"Unknown argument: {argument}");
                return;
            }

            DoArrival();
            DoAlign();
        }

        #region STOP
        void DoStop()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None; // Detener comprobaciones
            alignData.Clear();
            arrivalData.Clear();
            SaveToStorage();
            ResetGyros();
            ResetThrust();
            Echo("Stopped.");
        }
        #endregion

        #region ALIGN
        void InitializeAlign(string message)
        {
            alignData.Initialize(message);
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            SaveToStorage();
        }
        void DoAlign()
        {
            if (!alignData.HasTarget)
            {
                Echo("Align target undefined...");
                return;
            }

            if (alignData.CurrentTarget >= alignData.Waypoints.Count)
            {
                Echo("Destination reached.");
                ExcuteAction(alignData.Command);
                DoStop();
                return;
            }

            AlignToVectors(alignData.TargetForward, alignData.TargetUp, gyrosThr);

            var currentPos = connectorA.GetPosition();
            var currentVelocity = remoteAlign.GetShipVelocities().LinearVelocity;
            double mass = remoteAlign.CalculateShipMass().PhysicalMass;

            NavigateWaypoints(currentPos, currentVelocity, mass);
        }
        void NavigateWaypoints(Vector3D currentPos, Vector3D currentVelocity, double mass)
        {
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

            double desiredSpeed = alignData.CalculateDesiredSpeed(distance);
            var neededForce = Utils.CalculateThrustForce(toTarget, desiredSpeed, currentVelocity, mass);

            ApplyThrust(neededForce);
        }
        #endregion

        #region ARRIVAL
        void InitializeArrival(string message)
        {
            arrivalData.Initialize(message);
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            SaveToStorage();
        }
        void DoArrival()
        {
            if (!arrivalData.HasPosition)
            {
                Echo("Arrival position undefined...");
                return;
            }

            MonitorizeArrival();
        }
        void MonitorizeArrival()
        {
            double distance = Vector3D.Distance(remoteArrival.GetPosition(), arrivalData.TargetPosition);
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
        #endregion

        #region UTILITY
        T GetBlockWithName<T>(string name) where T : class, IMyTerminalBlock
        {
            List<T> blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid);

            return blocks.FirstOrDefault(b => b.CustomName.Contains(name));
        }

        void ApplyThrust(Vector3D force)
        {
            foreach (var t in thrusters)
            {
                var thrustDir = t.WorldMatrix.Backward;
                double alignment = thrustDir.Dot(force);

                t.Enabled = true;
                t.ThrustOverridePercentage = alignment > 0 ? (float)Math.Min(alignment / t.MaxEffectiveThrust, 1f) : 0f;
            }
        }
        void ResetThrust()
        {
            foreach (var t in thrusters)
            {
                t.Enabled = true;
                t.ThrustOverridePercentage = 0f;
            }
        }

        void AlignToVectors(Vector3D targetForward, Vector3D targetUp, double thr)
        {
            var shipMatrix = remoteAlign.WorldMatrix;
            var shipForward = shipMatrix.Forward;
            var shipUp = shipMatrix.Up;

            double angleFW = Utils.AngleBetweenVectors(shipForward, targetForward);
            double angleUP = Utils.AngleBetweenVectors(shipUp, targetUp);
            Echo($"Target angles: {angleFW:F2} | {angleUP:F2}");

            if (angleFW <= thr && angleUP <= thr)
            {
                ResetGyros();
                Echo("Aligned.");
                return;
            }
            Echo("Aligning...");

            if (angleFW > thr)
            {
                var rotationAxisFW = Vector3D.Cross(shipForward, targetForward);
                if (rotationAxisFW.Length() <= 0.001) rotationAxisFW = new Vector3D(0, 1, 0);
                ApplyGyroOverride(rotationAxisFW);
            }

            if (angleUP > thr)
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
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"UpdateFrequency={(int)Runtime.UpdateFrequency}",
                $"AlignData={alignData.SaveToStorage()}",
                $"ArrivalData={arrivalData.SaveToStorage()}",
            };

            Storage = string.Join(Environment.NewLine, parts);
        }
        #endregion
    }
}
