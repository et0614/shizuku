using System;
using System.Collections.Generic;
using System.IO.BACnet;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BaCSharp
{
  /// <summary>富樫作成。これで動作するか未検証</summary>
  public class Accumulator<T> : BaCSharpObject
  {
    public Accumulator(int ObjId, String ObjName, String Description, T InitialValue, BacnetUnitsId Unit)
            : base(new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)ObjId), ObjName, Description)
    {
      m_PRESENT_VALUE_ReadOnly = true;
      m_PROP_UNITS = (uint)Unit;
      m_PROP_PRESENT_VALUE = InitialValue;
    }

    public BacnetBitString m_PROP_STATUS_FLAGS = new BacnetBitString();
    [BaCSharpType(BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING)]
    public virtual BacnetBitString PROP_STATUS_FLAGS
    {
      get { return m_PROP_STATUS_FLAGS; }
    }

    [BaCSharpType(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED)]
    public virtual uint PROP_EVENT_STATE
    {
      get { return 0; }
    }

    public bool m_PRESENT_VALUE_ReadOnly = false;
    public T m_PROP_PRESENT_VALUE;
    // BacnetSerialize made freely by the stack depending on the type
    public virtual T PROP_PRESENT_VALUE
    {
      get { return m_PROP_PRESENT_VALUE; }
      set
      {
        if (m_PRESENT_VALUE_ReadOnly == false)
        {
          internal_PROP_PRESENT_VALUE = value;
        }
        else
          ErrorCode_PropertyWrite = ErrorCodes.WriteAccessDenied;
      }
    }

    // This property shows the same attribut as the previous, but without restriction
    // for internal usage, not for network callbacks
    public virtual T internal_PROP_PRESENT_VALUE
    {
      get { return m_PROP_PRESENT_VALUE; }
      set
      {
        if (!value.Equals(m_PROP_PRESENT_VALUE))
        {
          m_PROP_PRESENT_VALUE = value;
          ExternalCOVManagement(BacnetPropertyIds.PROP_PRESENT_VALUE);
          //IntrinsicReportingManagement();
        }
      }
    }

    public uint m_PROP_UNITS;
    [BaCSharpType(BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED)]
    public virtual uint PROP_UNITS
    {
      get { return m_PROP_UNITS; }
    }

  }
}
