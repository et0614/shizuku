import PresentValueReadWriter
import time
import asyncio

from enum import Enum

class EnvironmentCommunicator(PresentValueReadWriter.PresentValueReadWriter):

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

    def __init__(self, id, name='envComm', device_ip='127.0.0.1', emulator_ip='127.0.0.1', time_out_sec=1.0):
        """インスタンスを初期化する

        Args:
            id (int): 通信用のDeviceのID
            name (str): 通信用のDeviceの名前
            device_ip (str): 通信に使うDeviceのIP Address（xxx.xxx.xxx.xxx）
            emulator_ip (str): エミュレータのIP Address（xxx.xxx.xxx.xxx）
        """
        super().__init__(id, name, device_ip, emulator_ip, time_out_sec)
        self.target_ip = emulator_ip + ':' + str(self.ENVIRONMENTMONITOR_EXCLUSIVE_PORT)

    async def get_drybulb_temperature(self):
        """外気乾球温度[C]を取得する

        Returns:
            list: 読み取り成功の真偽,外気乾球温度[C]
        """
        return await self.read_present_value(self.target_ip,'analogInput:' + str(self._member.DrybulbTemperature.value))
    
    async def get_relative_humidity(self):
        """外気相対湿度[%]を取得する

        Returns:
            list: 読み取り成功の真偽,外気相対湿度[%]
        """
        return await self.read_present_value(self.target_ip,'analogInput:' + str(self._member.RelativeHumdity.value))
    
    async def get_global_horizontal_radiation(self):
        """水平面全天日射[W/m2]を取得する

        Returns:
            list: 読み取り成功の真偽,水平面全天日射[W/m2]
        """
        return await self.read_present_value(self.target_ip,'analogInput:' + str(self._member.GlobalHorizontalRadiation.value))
    
    async def get_nocturnal_radiation(self):
        """夜間放射[W/m2]を取得する

        Returns:
            list: 読み取り成功の真偽,夜間放射[W/m2]
        """
        return await self.read_present_value(self.target_ip,'analogInput:' + str(self._member.NocturnalRadiation.value))
    
    async def get_zone_drybulb_temperature(self,oUnitIndex,iUnitIndex):
        """ゾーン（下部空間）の乾球温度[C]を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～8）

        Returns:
            list: 読み取り成功の真偽,ゾーン（下部空間）の乾球温度[C]
        """        
        objNum = 1000 * oUnitIndex + 100 * iUnitIndex + self._member.DrybulbTemperature.value
        return await self.read_present_value(self.target_ip,'analogInput:' + str(objNum))

    async def get_zone_relative_humidity(self,oUnitIndex,iUnitIndex):
        """ゾーン（下部空間）の相対湿度[%]を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～8）

        Returns:
            list: 読み取り成功の真偽,ゾーン（下部空間）の相対湿度[%]
        """
        objNum = 1000 * oUnitIndex + 100 * iUnitIndex + self._member.RelativeHumdity.value
        return await self.read_present_value(self.target_ip,'analogInput:' + str(objNum))

async def main():
    wCom = EnvironmentCommunicator(14)

    while True:
        val = await wCom.get_drybulb_temperature()
        print('乾球温度' + ('= ' + '{:.1f}'.format(val[1]) + ' C' if val[0] else ' 通信失敗'))

        val = await wCom.get_relative_humidity()
        print('相対湿度' + ('= ' + '{:.1f}'.format(val[1]) + ' %' if val[0] else ' 通信失敗'))

        val = await wCom.get_global_horizontal_radiation()
        print('水平面全天日射' + ('= ' + '{:.1f}'.format(val[1]) + ' W/m2' if val[0] else ' 通信失敗'))

        val = await wCom.get_nocturnal_radiation()
        print('夜間放射' + ('= ' + '{:.1f}'.format(val[1]) + ' W/m2' if val[0] else ' 通信失敗'))

        val = await wCom.get_zone_drybulb_temperature(2,4)
        print('VRF2-4の温度' + ('= ' + '{:.1f}'.format(val[1]) + ' C' if val[0] else ' 通信失敗'))

        val = await wCom.get_zone_relative_humidity(2,4)
        print('VRF2-4の湿度' + ('= ' + '{:.1f}'.format(val[1]) + ' %' if val[0] else ' 通信失敗'))

        time.sleep(1)

if __name__ == "__main__":
    asyncio.run(main())