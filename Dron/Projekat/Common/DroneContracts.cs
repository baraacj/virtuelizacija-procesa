using System;
using System.ServiceModel;
using System.Runtime.Serialization;

namespace Common
{
    // ServiceContract za Dron telemetrijski servis
    [ServiceContract]
    public interface IDroneService
    {
        [OperationContract]
        [FaultContract(typeof(DataFormatFault))]
        [FaultContract(typeof(ValidationFault))]
        DroneServiceResponse StartSession(DroneSessionMeta meta);

        [OperationContract]
        [FaultContract(typeof(DataFormatFault))]
        [FaultContract(typeof(ValidationFault))]
        DroneServiceResponse PushSample(DroneSample sample);

        [OperationContract]
        DroneServiceResponse EndSession();
    }

    // DataContract za meta informacije sesije
    [DataContract]
    public class DroneSessionMeta
    {
        [DataMember]
        public double LinearAccelerationX { get; set; }

        [DataMember]
        public double LinearAccelerationY { get; set; }

        [DataMember]
        public double LinearAccelerationZ { get; set; }

        [DataMember]
        public double WindSpeed { get; set; }

        [DataMember]
        public double WindAngle { get; set; }

        [DataMember]
        public DateTime Time { get; set; }

        [DataMember]
        public string SessionId { get; set; }
    }

    // DataContract za pojedinačni uzorak telemetrije drona
    [DataContract]
    public class DroneSample
    {
        [DataMember]
        public double Time { get; set; }

        [DataMember]
        public double WindSpeed { get; set; }

        [DataMember]
        public double WindAngle { get; set; }

        [DataMember]
        public double BatteryVoltage { get; set; }

        [DataMember]
        public double BatteryCurrent { get; set; }

        [DataMember]
        public double PositionX { get; set; }

        [DataMember]
        public double PositionY { get; set; }

        [DataMember]
        public double PositionZ { get; set; }

        [DataMember]
        public double OrientationX { get; set; }

        [DataMember]
        public double OrientationY { get; set; }

        [DataMember]
        public double OrientationZ { get; set; }

        [DataMember]
        public double OrientationW { get; set; }

        [DataMember]
        public double VelocityX { get; set; }

        [DataMember]
        public double VelocityY { get; set; }

        [DataMember]
        public double VelocityZ { get; set; }

        [DataMember]
        public double AngularX { get; set; }

        [DataMember]
        public double AngularY { get; set; }

        [DataMember]
        public double AngularZ { get; set; }

        [DataMember]
        public double LinearAccelerationX { get; set; }

        [DataMember]
        public double LinearAccelerationY { get; set; }

        [DataMember]
        public double LinearAccelerationZ { get; set; }
    }

    // Fault Contracts za greške
    [DataContract]
    public class DataFormatFault
    {
        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string Details { get; set; }
    }

    [DataContract]
    public class ValidationFault
    {
        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string Field { get; set; }

        [DataMember]
        public string ExpectedRange { get; set; }
    }

    // Enum za status odgovora
    public enum SessionStatus
    {
        IN_PROGRESS,
        COMPLETED,
        ERROR
    }

    // Enum za tip odgovora
    public enum ResponseType
    {
        ACK,
        NACK
    }

    // Klasa za odgovor servera
    [DataContract]
    public class DroneServiceResponse
    {
        [DataMember]
        public ResponseType Type { get; set; }

        [DataMember]
        public SessionStatus Status { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string SessionId { get; set; }
    }
}
