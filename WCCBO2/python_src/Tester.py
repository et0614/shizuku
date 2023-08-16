import time
import BACnetCommunicator
import WeatherCommunicator

from bacpypes.primitivedata import  Enumerated, Real, Integer, Unsigned
from bacpypes.basetypes import DateTime

BACNET_DEVICE_ID = 999

def main():
    test_BACnetCommunicator()
    # test_WeatherCommunicator()

def test_WeatherCommunicator():
    wCom = WeatherCommunicator.WeatherCommunicator(BACNET_DEVICE_ID, 'wetherCom')

    val = wCom.get_drybulb_temperature()
    print('乾球温度= ' + str(val[1]) + ' C')

    val = wCom.get_relative_humidity()
    print('相対湿度= ' + str(val[1]) + ' %')

    val = wCom.get_global_horizontal_radiation()
    print('水平面全天日射= ' + str(val[1]) + ' W/m2')

    # 無限ループで待機
    while True:
        pass

def test_BACnetCommunicator():
    print('make device')
    master = BACnetCommunicator.BACnetCommunicator(BACNET_DEVICE_ID, 'myDevice')
    #time.sleep(1)
    #print('start device')
    #master.start_service()

    # Who is送信
    master.who_is()
    
    # 非同期でread property
    pValue = master.read_present_value_async('127.0.0.1:47809', 'analogOutput:2', Integer, my_call_back_read)
    pValue = master.read_present_value_async('127.0.0.1:47810', 'binaryOutput:1101', Enumerated, my_call_back_read)
    pValue = master.read_present_value_async('127.0.0.1:47810', 'multiStateOutput:1103', Unsigned, my_call_back_read)
    pValue = master.read_present_value_async('127.0.0.1:47810', 'analogValue:1105', Real, my_call_back_read)
    pValue = master.read_present_value_async('127.0.0.1:47812', 'analogInput:1', Real, my_call_back_read)
    pValue = master.read_present_value_async('127.0.0.1:47809', 'datetimeValue:1', DateTime, my_call_back_read)

    # 同期でread property
    pValue = master.read_present_value('127.0.0.1:47809', 'analogOutput:2', Integer)
    print('synchronously reading ' + ('success, value=' + str(pValue[1]) if pValue[0] else 'failed'))
    pValue = master.read_present_value('127.0.0.1:47810', 'binaryOutput:1101', Enumerated)
    print('synchronously reading ' + ('success, value=' + str(pValue[1]) if pValue[0] else 'failed'))

    # 非同期でwrite property
    master.write_present_value_async('127.0.0.1:47809', 'analogOutput:2', Integer(1), my_call_back_write)
    master.write_present_value_async('127.0.0.1:47810', 'binaryOutput:1101', Enumerated(0), my_call_back_write)
    master.write_present_value_async('127.0.0.1:47810', 'multiStateOutput:1103', Unsigned(1), my_call_back_write)

    # 同期でwrite property
    success = master.write_present_value('127.0.0.1:47809', 'analogOutput:2', Integer(1))
    print('synchronously writing ' + ('success' if pValue[0] else 'failed'))
    success = master.write_present_value('127.0.0.1:47810', 'binaryOutput:1101', Enumerated(0))
    print('synchronously writing ' + ('success' if pValue[0] else 'failed'))
    success = master.write_present_value('127.0.0.1:47810', 'multiStateOutput:1103', Unsigned(1))
    print('synchronously writing ' + ('success' if pValue[0] else 'failed'))

    # 無限ループで待機
    while True:
        pass

def my_call_back_read(addr, obj_id, success, value):
    if(success):
        print('success asynchronously writing ' + str(addr) + ' : ' + str(obj_id) + ', value=' + str(value))
    else:
        print('failed asynchronously writing ' + str(addr) + ' : ' + str(obj_id) + ', ' + str(value))
    return

def my_call_back_write(addr, obj_id, success, value):
    if(success):
        print('success asynchronously writing ' + str(addr) + ' : ' + str(obj_id))
    else:
        print('failed asynchronously writing ' + str(addr) + ' : ' + str(obj_id) + ', ' + str(value))
    return

if __name__ == "__main__":
    main()