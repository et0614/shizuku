import time
import asyncio

from enum import Enum
from bacpypes3.primitivedata import Enumerated, Unsigned

import sys,os
sys.path.append(os.path.dirname(__file__))
import DateTimeCommunicator

class VentilationSystemCommunicator(DateTimeCommunicator.DateTimeCommunicator):

# region 定数宣言

    VENTCTRL_DEVICE_ID = 6

    VENTCTRL_EXCLUSIVE_PORT = 0xBAC0 + VENTCTRL_DEVICE_ID

# endregion

# region 列挙型定義

    class _member(Enum):
        # 南側CO2濃度
        SouthCO2Level = 1
        # 北側CO2濃度
        NorthCO2Level = 2
        # 全熱交換器On/Off
        HexOnOff = 3
        # 全熱交換器バイパス有効無効
        HexBypassEnabled = 4
        # 全熱交換器ファン風量
        HexFanSpeed = 5

    class FanSpeed(Enum):
        # 弱
        Low = 1
        # 中
        Middle = 2
        # 強
        High = 3

# endregion

# region コンストラクタ

    def __init__(self, id, name='vntComm', device_ip='127.0.0.1', emulator_ip='127.0.0.1', time_out_sec=1.0):
        """インスタンスを初期化する

        Args:
            id (int): 通信用のDeviceのID
            name (str): 通信用のDeviceの名前
            device_ip (str): 通信に使うDeviceのIP Address（xxx.xxx.xxx.xxx）
            emulator_ip (str): エミュレータのIP Address（xxx.xxx.xxx.xxx）
        """
        super().__init__(id, name, device_ip, emulator_ip, time_out_sec)
        self.target_ip = emulator_ip + ':' + str(self.VENTCTRL_EXCLUSIVE_PORT)

# endregion

# region テナント別の処理

    async def get_south_tenant_CO2_level(self):
        """南側テナントのCO2濃度[ppm]を取得する
        Returns:
            list: 読み取り成功の真偽,南側テナントのCO2濃度[ppm]
        """        
        inst = 'analogInput:' + str(self._member.SouthCO2Level.value)
        return await self.read_present_value(self.target_ip,inst)
    

    async def get_north_tenant_CO2_level(self):
        """北側テナントのCO2濃度[ppm]を取得する
        Returns:
            list: 読み取り成功の真偽,北側テナントのCO2濃度[ppm]
        """        
        inst = 'analogInput:' + str(self._member.NorthCO2Level.value)
        return await self.read_present_value(self.target_ip,inst)

# endregion    

# region Hex

    async def start_ventilation(self, oUnitIndex, iUnitIndex):
        """換気（全熱交換器）を起動する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            bool:命令が成功したか否か
        """        
        inst = 'binaryOutput:' + self._get_instance_number(oUnitIndex,iUnitIndex,self._member.HexOnOff.value)
        return await self.write_present_value(self.target_ip,inst,Enumerated(1))
    

    async def stop_ventilation(self, oUnitIndex, iUnitIndex):
        """換気（全熱交換器）を停止する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            bool:命令が成功したか否か
        """        
        inst = 'binaryOutput:' + self._get_instance_number(oUnitIndex,iUnitIndex,self._member.HexOnOff.value)
        return await self.write_present_value(self.target_ip,inst,Enumerated(0))
    

    async def enable_bypass_control(self, oUnitIndex, iUnitIndex):
        """バイパス制御を有効にする
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            bool:命令が成功したか否か
        """        
        inst = 'binaryOutput:' + self._get_instance_number(oUnitIndex,iUnitIndex,self._member.HexBypassEnabled.value)
        return await self.write_present_value(self.target_ip,inst,Enumerated(1))
    

    async def disable_bypass_control(self, oUnitIndex, iUnitIndex):
        """バイパス制御を無効にする
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            bool:命令が成功したか否か
        """        
        inst = 'binaryOutput:' + self._get_instance_number(oUnitIndex,iUnitIndex,self._member.HexBypassEnabled.value)
        return await self.write_present_value(self.target_ip,inst,Enumerated(0))


    async def change_fan_speed(self, oUnitIndex, iUnitIndex, speed):
        """ファン風量を変える
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
            speed (FanSpeed): ファン風量
        Returns:
            bool:命令が成功したか否か
        """        
        inst = 'multiStateOutput:' + self._get_instance_number(oUnitIndex,iUnitIndex,self._member.HexFanSpeed.value)
        return await self.write_present_value(self.target_ip,inst,Unsigned(speed.value))
    

    async def get_fan_speed(self, oUnitIndex, iUnitIndex):
        """ファン風量を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            list(bool,FanSpeed): 読み取り成功の真偽,ファン風量
        """        
        inst = 'multiStateOutput:' + self._get_instance_number(oUnitIndex,iUnitIndex,self._member.HexFanSpeed.value)
        val = await self.read_present_value(self.target_ip,inst)

        return val[0], self.FanSpeed.Low if val[1] == 1 else (self.FanSpeed.Middle if val[1] == 2 else self.FanSpeed.High)

# endregion

# region 補助メソッド

    def _get_instance_number(self,oUnitIndex,iUnitIndex,mem_id):
        return str(1000 * oUnitIndex + 100 * iUnitIndex + mem_id)

# endregion


async def main():
    vCom = VentilationSystemCommunicator(1000)

    while True:
        val = await vCom.get_south_tenant_CO2_level()
        print('CO2 level of south tenant' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))
        
        val = await vCom.get_north_tenant_CO2_level()
        print('CO2 level of north tenant' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))

        print('turn on HEX1-1...',end='')
        rslt = await vCom.start_ventilation(1,1)
        print('success' if rslt[0] else 'failed')

        print('turn off HEX1-1...',end='')
        rslt = await vCom.stop_ventilation(1,1)
        print('success' if rslt[0] else 'failed')

        val = await vCom.get_fan_speed(1,1)
        print('Fan speed of HEX1-1' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))

        print('change fan speed of HEX1-1...',end='')
        fs = VentilationSystemCommunicator.FanSpeed.High if val[1] == VentilationSystemCommunicator.FanSpeed.Low else  VentilationSystemCommunicator.FanSpeed.Low
        rslt = await vCom.change_fan_speed(1,1,fs)
        print('success' if rslt[0] else 'failed')

        val = await vCom.get_fan_speed(1,1)
        print('Fan speed of HEX1-1' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))

        time.sleep(1)

if __name__ == "__main__":
    asyncio.run(main())