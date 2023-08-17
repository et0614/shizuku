import BACnetCommunicator

from enum import Enum
from bacpypes.primitivedata import ObjectIdentifier, Enumerated, Real, Integer, BitString, Boolean, Unsigned

class EnvironmentCommunicator():

# region 定数宣言

    ENVIRONMENTMONITOR_DEVICE_ID = 4

    ENVIRONMENTMONITOR_EXCLUSIVE_PORT = 0xBAC0 + ENVIRONMENTMONITOR_DEVICE_ID

# endregion

# region 列挙型定義

    class _member(Enum):
        # 乾球温度
        DrybulbTemperature=1
        # 相対湿度
        RelativeHumdity=2
        # 水平面全天日射
        GlobalHorizontalRadiation=3
        # 夜間放射
        NocturnalRadiation=4

# endregion

    def __init__(self, id, name='envComm', target_ip='127.0.0.1'):
        """インスタンスを初期化する

        Args:
            id (int): 通信用のDeviceのID
            name (str): 通信用のDeviceの名前
            ip_address (str): Environment MonitorのIP Address（xxx.xxx.xxx.xxx:port）
        """
        self.target_ip = target_ip + ':' + str(self.ENVIRONMENTMONITOR_EXCLUSIVE_PORT)
        self.comm = BACnetCommunicator.BACnetCommunicator(id,name)

    def start_service(self):
        """BACnet通信を開始する
        """
        self.comm.start_service()

    def end_service(self):
        """BACnet通信を終了する
        """        
        self.comm.end_service()

    def get_drybulb_temperature(self):
        """外気乾球温度[C]を取得する

        Returns:
            list: 読み取り成功の真偽,外気乾球温度[C]
        """        
        return self.comm.read_present_value(self.target_ip,'analogInput:' + str(self._member.DrybulbTemperature.value),Real)
    
    def get_relative_humidity(self):
        """外気相対湿度[%]を取得する

        Returns:
            list: 読み取り成功の真偽,外気相対湿度[%]
        """        
        return self.comm.read_present_value(self.target_ip,'analogInput:' + str(self._member.RelativeHumdity.value),Real)
    
    def get_global_horizontal_radiation(self):
        """水平面全天日射[W/m2]を取得する

        Returns:
            list: 読み取り成功の真偽,水平面全天日射[W/m2]
        """        
        return self.comm.read_present_value(self.target_ip,'analogInput:' + str(self._member.GlobalHorizontalRadiation.value),Real)
    
    def get_nocturnal_radiation(self):
        """夜間放射[W/m2]を取得する

        Returns:
            list: 読み取り成功の真偽,夜間放射[W/m2]
        """        
        return self.comm.read_present_value(self.target_ip,'analogInput:' + str(self._member.NocturnalRadiation.value),Real)
    
    def get_zone_drybulb_temperature(self,oUnitIndex,iUnitIndex):
        """ゾーン（下部空間）の乾球温度[C]を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～8）

        Returns:
            list: 読み取り成功の真偽,ゾーン（下部空間）の乾球温度[C]
        """
        objNum = 1000 * oUnitIndex + 100 * iUnitIndex + self._member.DrybulbTemperature.value
        return self.comm.read_present_value(self.target_ip,'analogInput:' + str(objNum),Real)

    def get_zone_relative_humidity(self,oUnitIndex,iUnitIndex):
        """ゾーン（下部空間）の相対湿度[%]を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～8）

        Returns:
            list: 読み取り成功の真偽,ゾーン（下部空間）の相対湿度[%]
        """
        objNum = 1000 * oUnitIndex + 100 * iUnitIndex + self._member.RelativeHumdity.value
        return self.comm.read_present_value(self.target_ip,'analogInput:' + str(objNum),Real)

def main():
    wCom = EnvironmentCommunicator(999)
    wCom.start_service()

    while True:
        val = wCom.get_drybulb_temperature()
        print('乾球温度= ' + '{:.1f}'.format(val[1]) + ' C')

        val = wCom.get_relative_humidity()
        print('相対湿度= ' + '{:.1f}'.format(val[1]) + ' %')

        val = wCom.get_global_horizontal_radiation()
        print('水平面全天日射= ' + '{:.1f}'.format(val[1]) + ' W/m2')

        val = wCom.get_nocturnal_radiation()
        print('夜間放射= ' + '{:.1f}'.format(val[1]) + ' W/m2')

        val = wCom.get_zone_drybulb_temperature(2,4)
        print('VRF2-4の温度= ' + '{:.1f}'.format(val[1]) + ' C')

        val = wCom.get_zone_relative_humidity(2,4)
        print('VRF2-4の湿度= ' + '{:.1f}'.format(val[1]) + ' %')

    # 無限ループで待機
    while True:
        pass

if __name__ == "__main__":
    main()