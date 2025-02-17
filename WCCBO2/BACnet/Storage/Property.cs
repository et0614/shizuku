namespace System.IO.BACnet.Storage;

[Serializable]
public class Property
{
  [XmlIgnore]
  public BacnetPropertyIds Id { get; set; }

  [XmlAttribute("Id")]
  public string IdText
  {
    get
    {
      return Id.ToString();
    }
    set
    {
      Id = (BacnetPropertyIds)Enum.Parse(typeof(BacnetPropertyIds), value);
    }
  }

  [XmlAttribute]
  public BacnetApplicationTags Tag { get; set; }

  [XmlElement]
  public string[] Value { get; set; }

  #region 追加メソッド

  public Property() { }

  public Property(BacnetPropertyIds id, BacnetApplicationTags tag, string[] value)
  {
    Id = id;
    Tag = tag;
    Value = value;
  }

  public Property(BacnetPropertyIds id, BacnetApplicationTags tag, string value)
  {
    Id = id;
    Tag = tag;
    Value = new string[] { value };
  }

  #endregion

  public static BacnetValue DeserializeValue(string value, BacnetApplicationTags type)
  {
    switch (type)
    {
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL:
        return value == ""
            ? new BacnetValue(type, null)
            : new BacnetValue(value);
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN:
        return new BacnetValue(type, bool.Parse(value));
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT:
        return new BacnetValue(type, uint.Parse(value));
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_SIGNED_INT:
        return new BacnetValue(type, int.Parse(value));
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL:
        return new BacnetValue(type, float.Parse(value, CultureInfo.InvariantCulture));
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_DOUBLE:
        return new BacnetValue(type, double.Parse(value, CultureInfo.InvariantCulture));
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_OCTET_STRING:
        try
        {
          return new BacnetValue(type, Convert.FromBase64String(value));
        }
        catch
        {
          return new BacnetValue(type, value);
        }
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_CONTEXT_SPECIFIC_DECODED:
        try
        {
          return new BacnetValue(type, Convert.FromBase64String(value));
        }
        catch
        {
          return new BacnetValue(type, value);
        }
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING:
        return new BacnetValue(type, value);
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING:
        return new BacnetValue(type, BacnetBitString.Parse(value));
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED:
        return new BacnetValue(type, uint.Parse(value));
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_DATE:
        return new BacnetValue(type, DateTime.Parse(value));
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_TIME:
        return new BacnetValue(type, DateTime.Parse(value));
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID:
        return new BacnetValue(type, BacnetObjectId.Parse(value));
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_READ_ACCESS_SPECIFICATION:
        return new BacnetValue(type, BacnetReadAccessSpecification.Parse(value));
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_PROPERTY_REFERENCE:
        return new BacnetValue(type, BacnetDeviceObjectPropertyReference.Parse(value));

      //追加
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME:
        return new BacnetValue(type, DateTime.ParseExact(value, "yyyy-MM-ddTHH:mm:ss.fff", null));
        //return new BacnetValue(type, DateTime.Parse(value));
        //return new BacnetValue(type, DateTime.Parse(value) DateTime.ParseExact(value, "dd-MM-yyyy HH:mm:ss", null)); //debug
      //追加ここまで

      default:
        return new BacnetValue(type, null);
    }
  }

  public static string SerializeValue(BacnetValue value, BacnetApplicationTags type)
  {
    switch (type)
    {
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL:
        return value.ToString(); // Modif FC
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL:
        return ((float)value.Value).ToString(CultureInfo.InvariantCulture);
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_DOUBLE:
        return ((double)value.Value).ToString(CultureInfo.InvariantCulture);
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_OCTET_STRING:
        return Convert.ToBase64String((byte[])value.Value);
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_CONTEXT_SPECIFIC_DECODED:
        {
          return value.Value is byte[]? Convert.ToBase64String((byte[])value.Value)
              : string.Join(";", ((BacnetValue[])value.Value)
                  .Select(v => SerializeValue(v, v.Tag)));
        }

      //追加
      case BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME:
        return ((DateTime)value.Value).ToString("yyyy-MM-ddTHH:mm:ss.fff");
      //追記ここまで
      default:
        return value.Value.ToString();
    }
  }

  [XmlIgnore]
  public IList<BacnetValue> BacnetValue
  {
    get
    {
      if (Value == null)
        return new BacnetValue[0];

      var ret = new BacnetValue[Value.Length];
      for (var i = 0; i < ret.Length; i++)
        ret[i] = DeserializeValue(Value[i], Tag);

      return ret;
    }
    set
    {
      Value = value.Select(v => SerializeValue(v, Tag)).ToArray();
    }
  }
}
