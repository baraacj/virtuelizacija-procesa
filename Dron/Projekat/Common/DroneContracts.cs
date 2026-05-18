using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace Common
{
    // =========================================================================
    // ZADATAK 1: Skica sistema i pravila protokola
    //
    //   Arhitektura: Klijent <-> WCF servis <-> Disk
    //
    //   Pravila slanja:
    //   (a) Svaka sesija ima meta-zaglavlje sa poljima:
    //       LinearAccelerationX/Y/Z, WindSpeed, WindAngle, Time
    //   (b) Sekvencijalno slanje - klijent prolazi for petljom kroz CSV
    //       i salje po jedan red
    //   (c) Poruke: StartSession, PushSample, EndSession
    //   (d) Server vraca ACK/NACK i status IN_PROGRESS/COMPLETED
    //   (e) Pragovi su u konfiguraciji: W_threshold, A_threshold, +-25%
    // =========================================================================

    // =========================================================================
    // ZADATAK 2: WCF ServiceContract sa operacijama
    // =========================================================================
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

    // =========================================================================
    // ZADATAK 1 + 2: DataContract za meta-zaglavlje sesije
    //   Polja: LinearAccelerationX/Y/Z, WindSpeed, WindAngle, Time
    // =========================================================================
    [DataContract]
    public class DroneSessionMeta
    {
        [DataMember] public string   SessionId           { get; set; }
        [DataMember] public DateTime Time                { get; set; }
        [DataMember] public double   LinearAccelerationX { get; set; }
        [DataMember] public double   LinearAccelerationY { get; set; }
        [DataMember] public double   LinearAccelerationZ { get; set; }
        [DataMember] public double   WindSpeed           { get; set; }
        [DataMember] public double   WindAngle           { get; set; }
    }

    // =========================================================================
    // ZADATAK 2: DataContract za jedan uzorak telemetrije - DroneSample
    //   Polja: LinearAccelerationX/Y/Z, WindSpeed, WindAngle, Time + ostala
    // =========================================================================
    [DataContract]
    public class DroneSample
    {
        [DataMember] public double Time                { get; set; }
        [DataMember] public double WindSpeed           { get; set; }
        [DataMember] public double WindAngle           { get; set; }
        [DataMember] public double BatteryVoltage      { get; set; }
        [DataMember] public double BatteryCurrent      { get; set; }
        [DataMember] public double PositionX           { get; set; }
        [DataMember] public double PositionY           { get; set; }
        [DataMember] public double PositionZ           { get; set; }
        [DataMember] public double OrientationX        { get; set; }
        [DataMember] public double OrientationY        { get; set; }
        [DataMember] public double OrientationZ        { get; set; }
        [DataMember] public double OrientationW        { get; set; }
        [DataMember] public double VelocityX           { get; set; }
        [DataMember] public double VelocityY           { get; set; }
        [DataMember] public double VelocityZ           { get; set; }
        [DataMember] public double AngularX            { get; set; }
        [DataMember] public double AngularY            { get; set; }
        [DataMember] public double AngularZ            { get; set; }
        [DataMember] public double LinearAccelerationX { get; set; }
        [DataMember] public double LinearAccelerationY { get; set; }
        [DataMember] public double LinearAccelerationZ { get; set; }
    }

    // =========================================================================
    // ZADATAK 3: Fault contracts za greske validacije i formata podataka
    // =========================================================================
    [DataContract]
    public class DataFormatFault
    {
        [DataMember] public string Message { get; set; }
        [DataMember] public string Details { get; set; }
    }

    [DataContract]
    public class ValidationFault
    {
        [DataMember] public string Message       { get; set; }
        [DataMember] public string Field         { get; set; }
        [DataMember] public string ExpectedRange { get; set; }
    }

    // =========================================================================
    // ZADATAK 1: ACK/NACK odgovor i IN_PROGRESS/COMPLETED status
    // =========================================================================
    public enum ResponseType  { ACK, NACK }
    public enum SessionStatus { IN_PROGRESS, COMPLETED, ERROR }

    [DataContract]
    public class DroneServiceResponse
    {
        [DataMember] public ResponseType  Type      { get; set; }
        [DataMember] public SessionStatus Status    { get; set; }
        [DataMember] public string        Message   { get; set; }
        [DataMember] public string        SessionId { get; set; }
    }
}
