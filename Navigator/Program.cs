using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Fase1 - Encarar al destino
    /// Fase2 - Acelerar hasta velocidad de crucero
    /// Fase3 - Mantener velocidad de crucero con thrusters apagados
    /// Fase4 - Frenado al llegar al destino
    /// Fase5 - Aviso de llegada
    ///
    /// Con la detección de un obstáculo, se activa el modo de esquivar.
    /// Fase1 - Localizar el obstáculo y obtener información de radio y distancia al obstáculo
    /// Fase2 - Frenar
    /// Fase3 - Calcular tres puntos de ruta de esquiva
    ///     Punto A: Desde el punto actual de la nave, con el vector UP de la nave multiplicado por el radio del obstáculo + margen de seguridad
    ///     Punto B: Desde el punto A, con el vector FORWARD de la nave multiplicado por la distancia al obstáculo* 2
    ///     Punto C: Desde el punto B, con el vector DOWN de la nave multiplicado por el radio del obstáculo + margen de seguridad
    /// Fase4 - Desplazarse siguiendo los puntos sin girar
    /// Fase5 - Aceleración
    /// </summary>
    partial class Program : MyGridProgram
    {
        #region Constants
        const string NavTag = "NAVIGATE|";
        const string StopTag = "STOP";
        const string shipRemoteControlNavigator = "HT Remote Control Pilot";
        const string shipCameraNavigator = "HT Camera Pilot";
        const string shipBeaconName = "HT Distress Beacon";

        //Velocidad de los giroscopios
        const double GyrosSpeed = 5f;

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
        readonly IMyRemoteControl remoteNavigator;
        readonly IMyCameraBlock cameraNavigator;
        readonly IMyBeacon beacon;
        readonly List<IMyThrust> thrusters;
        readonly List<IMyGyro> gyros;
        #endregion

        readonly NavigationData navigationData = new NavigationData();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;

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

            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (!string.IsNullOrEmpty(argument))
            {
                if (argument == StopTag)
                {
                    DoStop();
                    return;
                }
                if (argument.StartsWith(NavTag))
                {
                    InitializeNavigation(Utils.ReadArgument(argument, NavTag, '|'));
                    return;
                }

                Echo($"Unknown argument: {argument}");
                return;
            }

            DoNavigation();
        }

        void DoStop()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
            navigationData.Clear();
            SaveToStorage();
            ResetGyros();
            ResetThrust();
            BroadcastStatus("Navigation stopped.");
        }

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
        #endregion

        #region UTILITY
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

        void LoadFromStorage()
        {
            string[] storageLines = Storage.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (storageLines.Length == 0)
            {
                return;
            }

            Runtime.UpdateFrequency = (UpdateFrequency)Utils.ReadInt(storageLines, "UpdateFrequency");
            navigationData.LoadFromStorage(Utils.ReadString(storageLines, "NavigationData"));
        }
        void SaveToStorage()
        {
            List<string> parts = new List<string>
            {
                $"UpdateFrequency={(int)Runtime.UpdateFrequency}",
                $"NavigationData={navigationData.SaveToStorage()}",
            };

            Storage = string.Join(Environment.NewLine, parts);
        }
        #endregion
    }
}
