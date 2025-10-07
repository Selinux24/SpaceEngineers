using System;
using System.Collections.Generic;
using System.Text;
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
        public BoundingBoxD BoundingBox;
        public long EntityId;
        public Vector3D? HitPosition;
        public string Name;
        public MyDetectedEntityType Type;

        public Vector3D Position { get; private set; }

        public bool IsEmpty()
        {
            throw new NotImplementedException();
        }
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
        public double TotalMass { get; set; }
    }
    public struct MyIGCMessage
    {
        public object Data;
        public long Source;
        public string Tag;

        public MyIGCMessage(object data, string tag, long source)
        {
            Data = data;
            Tag = tag;
            Source = source;
        }

        public TData As<TData>()
        {
            return (TData)Data;
        }
    }
    public struct SerializableDefinitionId
    {
        public string SubtypeId { get; set; }
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
    public enum UpdateType
    {
        IGC,
        Mod,
        None,
        Once,
        Script,
        Terminal,
        Trigger,
        Update1,
        Update10,
        Update100,
    }
    public enum UpdateFrequency
    {
        None,
        Once,
        Update1,
        Update10,
        Update100,
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
        int CurrentInstructionCount { get; }
        int MaxInstructionCount { get; }
        UpdateFrequency UpdateFrequency { get; set; }
        TimeSpan TimeSinceLastRun { get; }
    }
    public interface IMyUnicastListener : IMyMessageProvider
    {

    }
    public interface IMyIntergridCommunicationSystem
    {
        long Me { get; }
        IMyUnicastListener UnicastListener { get; }

        IMyBroadcastListener RegisterBroadcastListener(string canal);
        void SendBroadcastMessage(string v, string msg);
        bool SendUnicastMessage<TData>(long addressee, string tag, TData data);
    }
    public interface IMyGridTerminalSystem
    {
        void GetBlocksOfType<T>(List<IMyTerminalBlock> blocks, Func<IMyTerminalBlock, bool> collect = null);
        void GetBlocksOfType<T>(List<T> blocks, Func<T, bool> collect = null) where T : class, IMyTerminalBlock;
        IMyTerminalBlock GetBlockWithName(string name);
    }

    public interface IMyMessageProvider
    {
        bool HasPendingMessage { get; }
        MyIGCMessage AcceptMessage();
        void SetMessageCallback(string v);
    }
    public interface IMyBroadcastListener : IMyMessageProvider
    {
    }

    public interface IMyTerminalBlock : IMyCubeBlock, IMyEntity
    {
        string CustomData { get; set; }
        string CustomName { get; set; }
    }
    public interface IMyFunctionalBlock : IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
        bool Enabled { get; set; }
    }
    public interface IMyShipController : IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
        MyShipMass CalculateShipMass();
        MyShipVelocities GetShipVelocities();
        Vector3D GetNaturalGravity();
        double GetShipSpeed();
    }
    public interface IMyPowerProducer : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
    }
    public interface IMyTextSurface
    {
        ContentType ContentType { get; set; }
        bool WriteText(string value, bool append = false);
        bool WriteText(StringBuilder value, bool append = false);
    }
    public interface IMyTextSurfaceProvider
    {
        IMyTextSurface GetSurface(int index);
    }

    public interface IMyProgrammableBlock : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity, IMyTextSurfaceProvider
    {
        void TryRun(string message);
    }
    public interface IMyRemoteControl : IMyShipController, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
        bool WaitForFreeWay { get; set; }
        FlightMode FlightMode { get; set; }
        float SpeedLimit { get; set; }

        void AddWaypoint(Vector3D destination, string destinationName);
        void ClearWaypoints();
        void SetAutoPilotEnabled(bool v);
        void SetCollisionAvoidance(bool v);
    }
    public interface IMyCameraBlock : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
        bool EnableRaycast { get; set; }

        bool CanScan(double collisionDetectRange);
        bool CanScan(double collisionDetectRange, Vector3D localDirection);
        MyDetectedEntityInfo Raycast(double collisionDetectRange);
        MyDetectedEntityInfo Raycast(double collisionDetectRange, Vector3D direction);
    }
    public interface IMyThrust : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
        float ThrustOverridePercentage { get; set; }
        float MaxEffectiveThrust { get; set; }
    }
    public interface IMyGyro : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
        bool GyroOverride { get; set; }
        float Pitch { get; set; }
        float Yaw { get; set; }
        float Roll { get; set; }
    }
    public interface IMyBeacon : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
    }
    public interface IMyLaserAntenna : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
        MyLaserAntennaStatus Status { get; set; }

        void Connect();
        void SetTargetCoords(string gpsReceptor);
    }
    public interface IMyCargoContainer : IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
    }
    public interface IMyTextPanel : IMyTextSurface, IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
    }
    public interface IMyShipConnector : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
        bool IsConnected { get; }
        MyShipConnectorStatus Status { get; set; }
        IMyShipConnector OtherConnector { get; }
    }
    public interface IMyConveyorSorter : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
    }
    public interface IMyRadioAntenna : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
    }
    public interface IMyBatteryBlock : IMyPowerProducer, IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
        float MaxStoredPower { get; }
        float CurrentStoredPower { get; }
    }
    public interface IMyGasTank : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
        float Capacity { get; }
        double FilledRatio { get; }
    }
    public interface IMyCockpit : IMyShipController, IMyTerminalBlock, IMyCubeBlock, IMyEntity, IMyTextSurfaceProvider
    {
    }
    public interface IMyDoor : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
        float OpenRatio { get; }
        void CloseDoor();
    }
    public interface IMyLightingBlock : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
        Color Color { get; set; }
        float BlinkIntervalSeconds { get; set; }
        float BlinkLength { get; set; }
    }
    public interface IMyAirtightDoorBase : IMyDoor, IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
    }
    public interface IMyAirtightHangarDoor : IMyAirtightDoorBase, IMyDoor, IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
    }
    public interface IMyMechanicalConnectionBlock : IMyFunctionalBlock, IMyTerminalBlock, IMyCubeBlock, IMyEntity
    {
        IMyCubeGrid TopGrid { get; }
    }
}
