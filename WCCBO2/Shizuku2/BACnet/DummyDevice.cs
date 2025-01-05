using BaCSharp;
using System.IO.BACnet.Storage;
using System.Reflection;

namespace Shizuku2.BACnet
{
  internal class DummyDevice : IBACnetController
  {

    #region 定数宣言

    /// <summary>Device ID</summary>
    public const uint DEVICE_ID = 9;

    /// <summary>排他的ポート番号</summary>
    public const int EXCLUSIVE_PORT = 0xBAC0 + (int)DEVICE_ID;

    const string DEVICE_NAME = "Dummy device";

    const string DEVICE_DESCRIPTION = "Dummy device to test BACnet communication.";

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>BACnet通信用オブジェクト</summary>
    private BACnetCommunicator communicator;

    public BACnetCommunicator Communicator { get { return communicator; } }

    #endregion

    #region 列挙型

    public enum MemberNumber
    {
      AnalogValueInt = 1,
      AnalogOutputInt = 2,
      AnalogInputInt = 3,
      AnalogValueReal = 4,
      AnalogOutputReal = 5,
      AnalogInputReal = 6,
      BinaryValue = 7,
      BinaryOutput = 8,
      BinaryInput = 9,
      MultiStateValue = 10,
      MultiStateOutput = 11,
      MultiStateInput = 12,
      DateTime = 13
    }

    #endregion

    #region コンストラクタ

    /// <summary>BACnet通信テストのためのダミーDevice</summary>
    /// <param name="localEndpointIP">エミュレータのIPアドレス</param>
    public DummyDevice(string localEndpointIP)
    {
      DeviceStorage strg = DeviceStorage.Load(
        new StreamReader
        (Assembly.GetExecutingAssembly().GetManifestResourceStream("Shizuku2.Resources.DummyDeviceStorage.xml"))
        );
      communicator = new BACnetCommunicator(strg, EXCLUSIVE_PORT, localEndpointIP);
    }

    #endregion

    #region IBACnetController実装

    public void ApplyManipulatedVariables(DateTime dTime) { }

    public void EndService()
    {
      communicator.EndService();
    }

    public void ReadMeasuredValues(DateTime dTime) { }

    public void StartService()
    {
      communicator.StartService();
    }

    #endregion

  }
}
