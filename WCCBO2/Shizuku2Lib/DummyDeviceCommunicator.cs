using System.Diagnostics.Metrics;
using System.IO.BACnet;
using System.IO.BACnet.Base;

namespace Shizuku2.BACnet
{
  /// <summary>Shizuku2のDummy Deviceとの通信ユーティリティクラス</summary>
  public class DummyDeviceCommunicator : PresentValueReadWriter
  {

    #region 定数宣言

    /// <summary>Device ID</summary>
    public const uint DUMMY_DEVICE_ID = 9;

    /// <summary>排他的ポート番号</summary>
    public const int DUMMY_DEVICE_EXCLUSIVE_PORT = 0xBAC0 + (int)DUMMY_DEVICE_ID;

    /// <summary>Dummy DeviceのBACnetアドレス</summary>
    private readonly BacnetAddress bacAddress;

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

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="id">通信に使うBACnet DeviceのID</param>
    /// <param name="name">通信に使うBACnet Deviceの名前</param>
    /// <param name="ipAddress">Dummy DeviceのIPアドレス（「xxx.xxx.xxx.xxx」の形式）</param>
    public DummyDeviceCommunicator(uint id, string name, string ipAddress = "127.0.0.1")
      : base(id, name)
    {
      bacAddress = new BacnetAddress(BacnetAddressTypes.IP, ipAddress + ":" + DUMMY_DEVICE_EXCLUSIVE_PORT.ToString());
    }

    #endregion

    #region インスタンスメソッド(Read)

    /// <summary>Analog Valueの値を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>Analog Valueの値</returns>
    public int ReadAnalogValueInt(out bool succeeded)
    {
      return ReadPresentValue<int>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)MemberNumber.AnalogValueInt, out succeeded);
    }

    /// <summary>Analog Outputの値を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>Analog Outputの値</returns>
    public int ReadAnalogOutputInt(out bool succeeded)
    {
      return ReadPresentValue<int>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)MemberNumber.AnalogOutputInt, out succeeded);
    }

    /// <summary>Analog Inputの値を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>Analog Inputの値</returns>
    public int ReadAnalogInputInt(out bool succeeded)
    {
      return ReadPresentValue<int>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.AnalogInputInt, out succeeded);
    }

    /// <summary>Analog Valueの値を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>Analog Valueの値</returns>
    public float ReadAnalogValueReal(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)MemberNumber.AnalogValueReal, out succeeded);
    }

    /// <summary>Analog Outputの値を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>Analog Outputの値</returns>
    public float ReadAnalogOutputReal(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_OUTPUT, (uint)MemberNumber.AnalogOutputReal, out succeeded);
    }

    /// <summary>Analog Inputの値を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>Analog Inputの値</returns>
    public float ReadAnalogInputReal(out bool succeeded)
    {
      return ReadPresentValue<float>
        (bacAddress, BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)MemberNumber.AnalogInputReal, out succeeded);
    }

    /// <summary>Binary Valueの値を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>Binary Valueの値</returns>
    public bool ReadBinaryValue(out bool succeeded)
    {
      return 1 == ReadPresentValue<uint>
        (bacAddress, BacnetObjectTypes.OBJECT_BINARY_VALUE, (uint)MemberNumber.BinaryValue, out succeeded);
    }

    /// <summary>Binary Outputの値を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>Binary Outputの値</returns>
    public bool ReadBinaryOutput(out bool succeeded)
    {
      return 1 == ReadPresentValue<uint>
        (bacAddress, BacnetObjectTypes.OBJECT_BINARY_OUTPUT, (uint)MemberNumber.BinaryOutput, out succeeded);
    }

    /// <summary>Binary Inputの値を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>Binary Inputの値</returns>
    public bool ReadBinaryInput(out bool succeeded)
    {
      return 1 == ReadPresentValue<uint>
        (bacAddress, BacnetObjectTypes.OBJECT_BINARY_INPUT, (uint)MemberNumber.BinaryInput, out succeeded);
    }

    /// <summary>MultiStateValueの値を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>MultiStateValueの値</returns>
    public uint ReadMultiStateValue(out bool succeeded)
    {
      return ReadPresentValue<uint>
        (bacAddress, BacnetObjectTypes.OBJECT_MULTI_STATE_VALUE, (uint)MemberNumber.MultiStateValue, out succeeded);
    }

    /// <summary>MultiStateOutputの値を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>MultiStateOutputの値</returns>
    public uint ReadMultiStateOutput(out bool succeeded)
    {
      return ReadPresentValue<uint>
        (bacAddress, BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT, (uint)MemberNumber.MultiStateOutput, out succeeded);
    }

    /// <summary>MultiStateInputの値を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>MultiStateInputの値</returns>
    public uint ReadMultiStateInput(out bool succeeded)
    {
      return ReadPresentValue<uint>
        (bacAddress, BacnetObjectTypes.OBJECT_MULTI_STATE_INPUT, (uint)MemberNumber.MultiStateInput, out succeeded);
    }

    /// <summary>DateTimeの値を取得する</summary>
    /// <param name="succeeded">通信が成功したか否か</param>
    /// <returns>DateTimeの値</returns>
    public DateTime ReadDateTime(out bool succeeded)
    {
      return ReadPresentValue<DateTime>
        (bacAddress, BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)MemberNumber.DateTime, out succeeded);
    }

    #endregion

    #region インスタンスメソッド(Write)

    /// <summary>Analog Valueの値を書き込む</summary>
    /// <param name="value">Analog Valueの値</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void WriteAnalogValue(int value, out bool succeeded)
    {
      WritePresentValue(bacAddress,
       BacnetObjectTypes.OBJECT_ANALOG_VALUE,
       (uint)MemberNumber.AnalogValueInt,
       new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_SIGNED_INT, value),
       out succeeded);
    }

    /// <summary>Analog Valueの値を書き込む</summary>
    /// <param name="value">Analog Valueの値</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void WriteAnalogValue(float value, out bool succeeded)
    {
      WritePresentValue(bacAddress,
       BacnetObjectTypes.OBJECT_ANALOG_VALUE,
       (uint)MemberNumber.AnalogValueReal,
       new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, value),
       out succeeded);
    }

    /// <summary>Analog Outputの値を書き込む</summary>
    /// <param name="value">Analog Outputの値</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void WriteAnalogOutput(int value, out bool succeeded)
    {
      WritePresentValue(bacAddress,
       BacnetObjectTypes.OBJECT_ANALOG_OUTPUT,
       (uint)MemberNumber.AnalogOutputInt,
       new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_SIGNED_INT, value),
       out succeeded);
    }

    /// <summary>Analog Outputの値を書き込む</summary>
    /// <param name="value">Analog Outputの値</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void WriteAnalogOutput(float value, out bool succeeded)
    {
      WritePresentValue(bacAddress,
       BacnetObjectTypes.OBJECT_ANALOG_OUTPUT,
       (uint)MemberNumber.AnalogOutputReal,
       new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, value),
       out succeeded);
    }

    /// <summary>Binary Valueの値を書き込む</summary>
    /// <param name="value">Binary Valueの値</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void WriteBinaryValue(bool value, out bool succeeded)
    {
      WritePresentValue(bacAddress,
       BacnetObjectTypes.OBJECT_BINARY_VALUE,
       (uint)MemberNumber.BinaryValue,
       new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED,
       value ? BacnetBinaryPv.BINARY_ACTIVE : BacnetBinaryPv.BINARY_INACTIVE),
       out succeeded);
    }

    /// <summary>Binary Outputの値を書き込む</summary>
    /// <param name="value">Binary Outputの値</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void WriteBinaryOutput(bool value, out bool succeeded)
    {
      WritePresentValue(bacAddress,
       BacnetObjectTypes.OBJECT_BINARY_OUTPUT,
       (uint)MemberNumber.BinaryOutput,
       new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED,
       value ? BacnetBinaryPv.BINARY_ACTIVE : BacnetBinaryPv.BINARY_INACTIVE),
       out succeeded);
    }

    /// <summary>MultiState Valueの値を書き込む</summary>
    /// <param name="value">MultiState Valueの値</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void WriteMultiStateValue(uint value, out bool succeeded)
    {
      WritePresentValue(bacAddress,
        BacnetObjectTypes.OBJECT_MULTI_STATE_VALUE,
       (uint)MemberNumber.MultiStateValue,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, value),
        out succeeded);
    }

    /// <summary>MultiState Outputの値を書き込む</summary>
    /// <param name="value">MultiState Outputの値</param>
    /// <param name="succeeded">通信が成功したか否か</param>
    public void WriteMultiStateOutput(uint value, out bool succeeded)
    {
      WritePresentValue(bacAddress,
        BacnetObjectTypes.OBJECT_MULTI_STATE_OUTPUT,
       (uint)MemberNumber.MultiStateOutput,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, value),
        out succeeded);
    }

    #endregion

  }
}
