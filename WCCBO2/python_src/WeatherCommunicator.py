import BACnetCommunicator

from enum import Enum
from bacpypes.primitivedata import ObjectIdentifier, Enumerated, Real, Integer, BitString, Boolean, Unsigned

class WeatherMonitorMember(Enum):
    # 乾球温度
    DrybulbTemperature=1
    # 相対湿度
    RelativeHumdity=2
    # 水平面全天日射
    GlobalHorizontalRadiation=3

class WeatherCommunicator():

    WEATHERMONITOR_DEVICE_ID = 4

    WEATHERMONITOR_EXCLUSIVE_PORT = 0xBAC0 + WEATHERMONITOR_DEVICE_ID

    def __init__(self, id, name='weatherComm', target_ip='127.0.0.1'):
        """インスタンスを初期化する

        Args:
            id (int): 通信用のDeviceのID
            name (str): 通信用のDeviceの名前
            ip_address (str): Weather MonitorのIP Address（xxx.xxx.xxx.xxx:port）
        """
        self.target_ip = target_ip + ':' + str(self.WEATHERMONITOR_EXCLUSIVE_PORT)
        self.comm = BACnetCommunicator.BACnetCommunicator(id,name)

    def get_drybulb_temperature(self):
        """外気乾球温度[C]を取得する

        Returns:
            list: 読み取り成功の真偽,外気乾球温度[C]
        """        
        val = self.comm.read_present_value(self.target_ip,'analogInput:' + str(WeatherMonitorMember.DrybulbTemperature.value),Real)
        return val
    
    def get_relative_humidity(self):
        """外気相対湿度[%]を取得する

        Returns:
            list: 読み取り成功の真偽,外気相対湿度[%]
        """        
        val = self.comm.read_present_value(self.target_ip,'analogInput:' + str(WeatherMonitorMember.RelativeHumdity.value),Real)
        return val
    
    def get_global_horizontal_radiation(self):
        """水平面全天日射[W/m2]を取得する

        Returns:
            list: 読み取り成功の真偽,水平面全天日射[W/m2]
        """        
        val = self.comm.read_present_value(self.target_ip,'analogInput:' + str(WeatherMonitorMember.GlobalHorizontalRadiation.value),Real)
        return val

