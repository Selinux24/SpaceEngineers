using System;
using System.Collections.Generic;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace Sandbox.ModAPI.Ingame
{
    public class MyGridProgram
    {
        public IMyGridTerminalSystem GridTerminalSystem { get; }
        public IMyIntergridCommunicationSystem IGC { get; }
        public IMyProgrammableBlock Me { get; }
        public IMyGridProgramRuntimeInfo Runtime { get; }
        public string Storage { get; set; }

        public Action<string> Echo { get; protected set; }
    }

    public struct BoundingBoxD
    {
        public Vector3D Extents { get; set; }
        public Vector3D Center { get; set; }
        public double Perimeter { get; set; }
    }
    public struct MyDetectedEntityInfo
    {
        public Vector3D? HitPosition { get; set; }
        public string Name { get; set; }
        public BoundingBoxD BoundingBox { get; set; }
        public Vector3D Position { get; set; }
        public MyDetectedEntityType Type { get; set; }

        public bool IsEmpty()
        {
            throw new NotImplementedException();
        }
    }
    public enum MyDetectedEntityType
    {
        Asteroid,
        CharacterHuman,
        CharacterOther,
        FloatingObject,
        LargeGrid,
        Meteor,
        Missile,
        None,
        Planet,
        SmallGrid,
        Unknown,
    }
    public struct MatrixD
    {
        public Vector3D Forward { get; }
        public Vector3D Backward { get; set; }
        public Vector3D Up { get; set; }

        public static MatrixD Invert(MatrixD worldMatrix)
        {
            throw new NotImplementedException();
        }
        public static MatrixD Transpose(MatrixD worldMatrix)
        {
            throw new NotImplementedException();
        }
    }
    public struct MyShipVelocities
    {
        public Vector3D LinearVelocity { get; set; }
    }
    public struct MyShipMass
    {
        public double PhysicalMass { get; set; }
    }
    public struct MyIGCMessage
    {
        public object Data { get; set; }
    }

    public enum UpdateType
    {
        IGC,
        Update1,
        Update100,
    }
    public enum UpdateFrequency
    {
        Update1,
        Update100,
        None,
    }
    public enum FlightMode
    {
        OneWay,
    }
    public enum MyLaserAntennaStatus
    {
        Connected,
        SearchingTargetForAntenna,
        RotatingToTarget,
        Connecting,
    }
    public enum MyShipConnectorStatus
    {
        Unconnected,
        Connected
    }

    public interface IMyGridProgramRuntimeInfo
    {
        UpdateFrequency UpdateFrequency { get; set; }
        TimeSpan TimeSinceLastRun { get; }
    }
    public interface IMyIntergridCommunicationSystem
    {
        IMyBroadcastListener RegisterBroadcastListener(string canal);
        void SendBroadcastMessage(string v, string msg);
    }
    public interface IMyGridTerminalSystem
    {
        void GetBlocksOfType<T>(List<T> blocks, Func<IMyTerminalBlock, bool> collect = null) where T : class, IMyTerminalBlock;
        IMyTerminalBlock GetBlockWithName(string name);
    }

    public interface IMyTerminalBlock
    {
        bool Enabled { get; set; }
        string CustomName { get; set; }
        IMyTerminalBlock CubeGrid { get; set; }
    }
    public interface IMyProgrammableBlock : IMyTerminalBlock
    {
        string CustomData { get; set; }

        void TryRun(string message);
    }
    public interface IMyRemoteControl : IMyTerminalBlock
    {
        MatrixD WorldMatrix { get; }
        bool WaitForFreeWay { get; set; }
        FlightMode FlightMode { get; set; }
        float SpeedLimit { get; set; }

        void AddWaypoint(Vector3D destination, string destinationName);
        MyShipMass CalculateShipMass();
        void ClearWaypoints();
        Vector3D GetPosition();
        MyShipVelocities GetShipVelocities();
        void SetAutoPilotEnabled(bool v);
        void SetCollisionAvoidance(bool v);
        Vector3D GetNaturalGravity();
        double GetShipSpeed();
    }
    public interface IMyCameraBlock : IMyTerminalBlock
    {
        bool EnableRaycast { get; set; }
        MatrixD WorldMatrix { get; set; }

        bool CanScan(double collisionDetectRange);
        bool CanScan(double collisionDetectRange, Vector3D localDirection);
        Vector3D GetPosition();
        MyDetectedEntityInfo Raycast(double collisionDetectRange);
        MyDetectedEntityInfo Raycast(double collisionDetectRange, Vector3D direction);
    }
    public interface IMyThrust : IMyTerminalBlock
    {
        MatrixD WorldMatrix { get; }
        float ThrustOverridePercentage { get; set; }
        float MaxEffectiveThrust { get; set; }
    }
    public interface IMyGyro : IMyTerminalBlock
    {
        MatrixD WorldMatrix { get; }
        bool GyroOverride { get; set; }
        float Pitch { get; set; }
        float Yaw { get; set; }
        float Roll { get; set; }
    }
    public interface IMyBeacon : IMyTerminalBlock
    {
    }
    public interface IMyLaserAntenna : IMyTerminalBlock
    {
        MyLaserAntennaStatus Status { get; set; }

        void Connect();
        void SetTargetCoords(string gpsReceptor);
    }
    public interface IMyBroadcastListener : IMyTerminalBlock
    {
        bool HasPendingMessage { get; }

        MyIGCMessage AcceptMessage();
        void SetMessageCallback(string v);
    }
    public interface IMyCargoContainer : IMyTerminalBlock
    {
        IMyInventory GetInventory();
    }
    public interface IMyTextPanel : IMyTerminalBlock
    {
        ContentType ContentType { get; set; }
        string CustomData { get; }

        void WriteText(string text, bool v);
        void WriteText(string v);
    }
    public interface IMyShipConnector : IMyTerminalBlock
    {
        MyShipConnectorStatus Status { get; set; }
        MatrixD WorldMatrix { get; set; }
        IMyShipConnector OtherConnector { get; }

        Vector3D GetPosition();
    }
    public interface IMyConveyorSorter : IMyTerminalBlock
    {

    }
    public interface IMyRadioAntenna : IMyTerminalBlock
    {

    }
}
