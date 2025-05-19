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

        const string shipRemoteControlNavigator = "HT Remote Control Pilot";
        const string shipCameraNavigator = "HT Camera Pilot";
        const string shipBeaconName = "HT Distress Beacon";

        const double gyrosThr = 0.001; //Precisión de alineación
        const double gyrosSpeed = 2f; //Velocidad de los giroscopios

        const double arrivalThr = 0.5; //Precisión de aproximación 0.5 metros
        const double arrivalThreshold = 200.0;

        // Velocidad máxima de crucero
        const double MaxSpeed = 100.0;
        const double MaxSpeedTrh = 0.95;

        // TODO: A una distancia del destino, bajar la velocidad de crucero a la mitad
        // TODO: Las correcciones de trayectoria en el modo de crucero deben hacerse con los propulsores pequeños
        // TODO: Cuando se quede a un porcentaje de batería, parar y esperar a que se recargue
        // TODO: Cuando esté en espera de cargarse, al llegar a un porcentaje de batería, continuar el viaje

        // Tiempo de encendido de thrusters hasta alineación
        const double ThrustSeconds = 5.0;
        // Precisión de alineación
        const double AlignThr = 0.001;
        // Precisión de alineación
        const double CruiseAlignThr = 0.01;

        // Rango de frenado hasta el objetivo
        const double ToTargetBrakingDistance = 500.0;

        // Rango de detección de colisiones
        const double CollisionDetectRange = 2500.0;
        const double EvadingWaypointDistance = 100.0;
        const double EvadingMaxSpeed = 19.5;
        #endregion

        #region Blocks
        readonly IMyProgrammableBlock pb;

        readonly IMyRemoteControl remoteArrival;

        readonly IMyRemoteControl remoteAlign;
        readonly IMyShipConnector connectorA;

        readonly IMyRemoteControl remoteNavigator;
        readonly IMyCameraBlock cameraNavigator;
        readonly IMyBeacon beacon;

        readonly List<IMyThrust> thrusters = new List<IMyThrust>();
        readonly List<IMyGyro> gyros = new List<IMyGyro>();
        #endregion

        readonly AlignData alignData = new AlignData();
        readonly ArrivalData arrivalData = new ArrivalData();
        readonly NavigationData navigationData = new NavigationData();

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

            remoteNavigator = GetBlockWithName<IMyRemoteControl>(shipRemoteControlNavigator);
            if (remoteNavigator == null)
            {
                Echo($"Remote Control {shipRemoteControlNavigator} not found.");
                return;
            }
            cameraNavigator = GetBlockWithName<IMyCameraBlock>(shipCameraNavigator);
            if (cameraNavigator == null)
            {
                Echo($"Camera {shipCameraNavigator} not found.");
                return;
            }
            beacon = GetBlockWithName<IMyBeacon>(shipBeaconName);
            if (beacon == null)
            {
                Echo($"Beacon {shipBeaconName} not found.");
                return;
            }

            gyros = GetBlocksOfType<IMyGyro>();
            if (gyros.Count == 0)
            {
                Echo("Grid without gyroscopes.");
                return;
            }
            thrusters = GetBlocksOfType<IMyThrust>();
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
                if (argument.StartsWith("NAVIGATE"))
                {
                    InitializeNavigation(Utils.ReadArgument(argument, "NAVIGATE", '|'));
                    return;
                }

                Echo($"Unknown argument: {argument}");
                return;
            }

            DoArrival();
            DoAlign();
            DoNavigation();
        }

        #region STOP
        void DoStop()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None; // Detener comprobaciones
            alignData.Clear();
            arrivalData.Clear();
            navigationData.Clear();
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

        #region NAVIGATOR
        void InitializeNavigation(string message)
        {
            navigationData.Initialize(message);
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
            SaveToStorage();
            BroadcastStatus("Starting navigation.");
        }
        void DoNavigation()
        {
            Echo($"State {navigationData.CurrentState}");

            if (!navigationData.HasTarget || navigationData.CurrentState == NavState.Idle)
            {
                /*
                IsObstacleAhead();
                if (!lastHit.IsEmpty())
                {
                    evadingPoints.Clear();
                    if (CalculateEvadingWaypoints())
                    {
                        Echo($"Obstacle center {lastHit.Position}");
                        Echo($"Navigating to waypoint {VecToStr(evadingPoints[0])}");
                        Echo($"Current position {VecToStr(camera.GetPosition())}");
                        Echo($"Distance to waypoint {Vector3D.Distance(evadingPoints[0], camera.GetPosition()):F2}m.");

                        // Mostrar en formato GPS
                        Echo($"GPS:Ship:{camera.GetPosition().X:F2}:{camera.GetPosition().Y:F2}:{camera.GetPosition().Z:F2}:#FFAAFF");
                        Echo($"GPS:Obstacle:{lastHit.Position.X:F2}:{lastHit.Position.Y:F2}:{lastHit.Position.Z:F2}:#FFAAFF");
                        for (int i = 0; i < evadingPoints.Count; i++)
                        {
                            var wp = evadingPoints[i];
                            Echo($"GPS:WP_{i}:{wp.X:F2}:{wp.Y:F2}:{wp.Z:F2}:#FFAAFF");
                        }
                    }
                }
                */
                Echo("Waiting for position...");
                return;
            }

            navigationData.UpdatePosition(cameraNavigator.GetPosition());

            Echo($"To target: {navigationData.DistanceToTarget:F2}m.");
            Echo(navigationData.PrintObstacle());

            switch (navigationData.CurrentState)
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

            SaveToStorage();
        }
        void Distress()
        {
            if (beacon != null) beacon.Enabled = true;
            BroadcastStatus($"DISTRESS: Engines damaged!, waiting in position {Utils.VectorToStr(remoteNavigator.GetPosition())}");
            ResetThrust();
            ResetGyros();
        }
        void Locate()
        {
            if (AlignToDirection(remoteNavigator.WorldMatrix.Forward, AlignThr))
            {
                BroadcastStatus("Destination located. Initializing acceleration.");
                navigationData.CurrentState = NavState.Accelerating;

                return;
            }

            ResetThrust();
        }
        void Accelerate()
        {
            if (navigationData.IsObstacleAhead(cameraNavigator, CollisionDetectRange))
            {
                BroadcastStatus("Obstacle detected. Avoiding...");
                navigationData.CurrentState = NavState.Avoiding;
                return;
            }

            if (navigationData.DistanceToTarget < ToTargetBrakingDistance)
            {
                BroadcastStatus("Destination reached. Braking.");
                navigationData.CurrentState = NavState.Braking;

                return;
            }

            var shipVelocity = remoteNavigator.GetShipVelocities().LinearVelocity.Length();
            if (shipVelocity >= MaxSpeed * MaxSpeedTrh)
            {
                BroadcastStatus("Reached cruise speed. Deactivating thrusters.");
                navigationData.CurrentState = NavState.Cruising;
                return;
            }

            // Acelerar
            ThrustToTarget(MaxSpeed);
        }
        void Cruise()
        {
            if (navigationData.IsObstacleAhead(cameraNavigator, CollisionDetectRange))
            {
                BroadcastStatus("Obstacle detected. Avoiding...");
                navigationData.CurrentState = NavState.Avoiding;

                return;
            }

            if (navigationData.DistanceToTarget < ToTargetBrakingDistance)
            {
                BroadcastStatus("Destination reached. Braking.");
                navigationData.CurrentState = NavState.Braking;

                return;
            }

            // Mantener velocidad
            if (!AlignToDirection(remoteNavigator.WorldMatrix.Forward, CruiseAlignThr))
            {
                // Encender los propulsores hasta alinear de nuevo el vector velocidad con el vector hasta el objetivo
                ThrustToTarget(MaxSpeed);
                navigationData.AlignThrustStart = DateTime.Now;
                navigationData.Thrusting = true;

                return;
            }

            if (navigationData.Thrusting)
            {
                // Propulsores encendidos para recuperar la alineación
                if ((DateTime.Now - navigationData.AlignThrustStart).TotalSeconds > ThrustSeconds)
                {
                    // Tiempo de alineación consumido. Desactivar propulsores
                    EnterCruising();
                    navigationData.Thrusting = false;
                }

                return;
            }

            var shipVelocity = remoteNavigator.GetShipVelocities().LinearVelocity.Length();
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
                ThrustToTarget(MaxSpeed);

                return;
            }

            EnterCruising();
        }
        void Brake()
        {
            ResetThrust();
            ResetGyros();

            var shipVelocity = remoteNavigator.GetShipVelocities().LinearVelocity.Length();
            if (shipVelocity <= 0.1)
            {
                BroadcastStatus("Destination reached.");
                navigationData.CurrentState = NavState.Idle;
            }
        }
        void Avoid()
        {
            if (navigationData.DistanceToTarget < ToTargetBrakingDistance)
            {
                BroadcastStatus("Destination reached. Braking.");
                navigationData.CurrentState = NavState.Braking;

                return;
            }

            // Calcular los puntos de evasión
            if (!navigationData.CalculateEvadingWaypoints(cameraNavigator))
            {
                //No se puede calcular un punto de evasión
                navigationData.CurrentState = NavState.Braking;

                return;
            }

            // Navegar entre los puntos de evasión
            if (navigationData.EvadingPoints.Count > 0)
            {
                NavigateTo(navigationData.EvadingPoints[0], EvadingMaxSpeed);

                if (navigationData.EvadingPoints.Count == 0)
                {
                    // Limpiar la información del obstáculo
                    navigationData.ClearObstacle();

                    ResetThrust();

                    // Volver a navegación cuando se alcance el último punto de navegación
                    navigationData.CurrentState = NavState.Locating;
                }

                return;
            }
        }
        void NavigateTo(Vector3D wayPoint, double maxSpeed)
        {
            var d = Vector3D.Distance(wayPoint, cameraNavigator.GetPosition());
            if (d <= EvadingWaypointDistance)
            {
                // Waypoint alcanzado
                navigationData.EvadingPoints.RemoveAt(0);

                return;
            }

            Echo($"Evading route...");
            Echo($"Distance to waypoint {d:F2}m.");

            ThrustToPosition(wayPoint, maxSpeed);
        }
        void EnterCruising()
        {
            ResetThrust();
            StopThrust();
            ResetGyros();
        }
        #endregion

        #region UTILITY
        T GetBlockWithName<T>(string name) where T : class, IMyTerminalBlock
        {
            List<T> blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid);

            return blocks.FirstOrDefault(b => b.CustomName.Contains(name));
        }
        List<T> GetBlocksOfType<T>() where T : class, IMyTerminalBlock
        {
            var blocks = new List<T>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid);
            return blocks.ToList();
        }

        void BroadcastStatus(string msg)
        {
            IGC.SendBroadcastMessage("NAV_STATUS", msg);
            Echo(msg);
        }

        void ThrustToTarget(double maxSpeed)
        {
            var currentVelocity = remoteNavigator.GetShipVelocities().LinearVelocity;
            double mass = remoteNavigator.CalculateShipMass().PhysicalMass;
            var force = Utils.CalculateThrustForce(navigationData.DirectionToTarget, maxSpeed, currentVelocity, mass);
            ApplyThrust(force);
        }
        void ThrustToPosition(Vector3D position, double maxSpeed)
        {
            // Vector normalizado desde la cámara al objetivo
            var toTarget = Vector3D.Normalize(position - cameraNavigator.GetPosition());
            var currentVelocity = remoteNavigator.GetShipVelocities().LinearVelocity;
            double mass = remoteNavigator.CalculateShipMass().PhysicalMass;

            var force = Utils.CalculateThrustForce(toTarget, maxSpeed, currentVelocity, mass);
            ApplyThrust(force);
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
        void StopThrust()
        {
            foreach (var t in thrusters)
            {
                t.Enabled = false;
                t.ThrustOverridePercentage = 0;
            }
        }

        bool AlignToDirection(Vector3D direction, double thr)
        {
            double angle = Utils.AngleBetweenVectors(direction, navigationData.DirectionToTarget);
            Echo($"Alineación: {angle:F4}");

            var rotationAxis = Vector3D.Cross(direction, navigationData.DirectionToTarget);
            if (Utils.IsZero(rotationAxis, thr))
            {
                ResetGyros();
                Echo("Alineado con el objetivo.");
                return true;
            }

            Echo("Alineando...");
            ApplyGyroOverride(rotationAxis);

            return false;
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
