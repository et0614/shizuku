import asyncio
import PresentValueReadWriter

from enum import Enum
from bacpypes3.primitivedata import Enumerated, Real, Unsigned

class VRFSystemCommunicator(PresentValueReadWriter.PresentValueReadWriter):

# region 定数宣言

    VRFCTRL_DEVICE_ID = 2

    VRFCTRL_EXCLUSIVE_PORT = 0xBAC0 + VRFCTRL_DEVICE_ID

# endregion

# region 列挙型定義

    class _member(Enum):
        # On/Offの設定
        OnOff_Setting = 1
        # On/Offの状態
        OnOff_Status = 2
        # 運転モードの設定
        OperationMode_Setting = 3
        # 運転モードの状態
        OperationMode_Status = 4
        # 室温設定値の設定
        Setpoint_Setting = 5
        # 室温設定値の状態
        Setpoint_Status = 6
        # 還乾球温度
        MeasuredRoomTemperature = 7
        # 還相対湿度
        MeasuredRelativeHumidity = 8
        # ファン風量の設定
        FanSpeed_Setting = 9
        # ファン風量の状態
        FanSpeed_Status = 10
        # 風向の設定
        AirflowDirection_Setting = 11
        # 風量の状態
        AirflowDirection_Status = 12
        # 手元リモコン操作許可の設定
        RemoteControllerPermittion_Setpoint_Setting = 13
        # 手元リモコン操作許可の状態
        RemoteControllerPermittion_Setpoint_Status = 14
        # 冷媒温度強制制御の設定
        ForcedRefrigerantTemperature_Setting = 15
        # 冷媒温度強制制御の状態
        ForcedRefrigerantTemperature_Status = 16
        # 冷媒蒸発温度設定値の設定
        EvaporatingTemperatureSetpoint_Setting = 17
        # 冷媒蒸発温度設定値の状態
        EvaporatingTemperatureSetpoint_Status = 18
        # 冷媒凝縮温度設定値の設定
        CondensingTemperatureSetpoint_Setting = 19
        # 冷媒凝縮温度設定値の状態
        CondensingTemperatureSetpoint_Status = 20
        # 消費電力
        Electricity = 21
        # 熱負荷
        HeatLoad = 22

    class Mode(Enum):
        # 冷却
        Cooling = 1
        # 加熱
        Heating = 2
        # サーモオフ
        ThermoOff = 3

    class FanSpeed(Enum):
        # 低
        Low = 1
        # 中
        Middle = 2
        # 高
        High = 3

    class Direction(Enum):
        # 水平
        Horizontal = 1
        # 22.5度
        Degree_225 = 2
        # 45.0度
        Degree_450 = 3
        # 67.5度
        Degree_675 = 4
        # 垂直
        Vertical = 5

# endregion

    def __init__(self, id, name='vrfComm', device_ip='127.0.0.1', emulator_ip='127.0.0.1', time_out_sec=1.0):
        """インスタンスを初期化する

        Args:
            id (int): 通信用のDeviceのID
            name (str): 通信用のDeviceの名前
            device_ip (str): 通信に使うDeviceのIP Address（xxx.xxx.xxx.xxx）
            emulator_ip (str): エミュレータのIP Address（xxx.xxx.xxx.xxx）
        """
        super().__init__(id, name, device_ip, emulator_ip, time_out_sec)
        self.target_ip = emulator_ip + ':' + str(self.VRFCTRL_EXCLUSIVE_PORT)

# region 発停関連

    async def turn_on(self, oUnitIndex, iUnitIndex):
        """室内機を起動する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            bool:命令が成功したか否か
        """        
        inst = 'binaryOutput:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.OnOff_Setting.value)
        return await self.write_present_value(self.target_ip,inst,Enumerated(1))

    async def turn_off(self, oUnitIndex, iUnitIndex):
        """室内機を停止する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            bool:命令が成功したか否か
        """        
        inst = 'binaryOutput:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.OnOff_Setting.value)
        return await self.write_present_value(self.target_ip,inst,Enumerated(0))

    async def is_turned_on(self, oUnitIndex, iUnitIndex):
        """起動しているか否か
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            list(bool,bool): 読み取り成功の真偽,起動しているか否か
        """
        inst = 'binaryInput:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.OnOff_Status.value)
        val = await self.read_present_value(self.target_ip,inst)
        return val[0], (val[1] == 1)

# endregion

# region 運転モード関連

    async def change_mode(self, oUnitIndex, iUnitIndex, mode):
        """運転モードを変える
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
            mode (Mode): 運転モード
        Returns:
            bool:命令が成功したか否か
        """        
        inst = 'multiStateOutput:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.OperationMode_Setting.value)
        return await self.write_present_value(self.target_ip,inst,Unsigned(mode.value))

    async def get_mode(self, oUnitIndex, iUnitIndex):
        """運転モードを取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            list(bool,Mode): 読み取り成功の真偽,運転モード
        """        
        inst = 'multiStateInput:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.OperationMode_Status.value)
        val = await self.read_present_value(self.target_ip,inst)
        return val[0], self.Mode.Cooling if val[1] == 1 else (self.Mode.Heating if val[1] == 2 else self.Mode.ThermoOff)

# endregion

# region 室温関連

    async def change_setpoint_temperature(self, oUnitIndex, iUnitIndex, sp):
        """室温設定値[C]を変える
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
            sp (float): 室温設定値[C]
        Returns:
            bool:命令が成功したか否か
            """
        inst = 'analogValue:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.Setpoint_Setting.value)
        return await self.write_present_value(self.target_ip,inst,Real(sp))
    
    async def get_setpoint_temperature(self, oUnitIndex, iUnitIndex):
        """室温設定値[C]を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            list(bool,float): 読み取り成功の真偽,室温設定値[C]
        """
        inst = 'analogInput:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.Setpoint_Status.value)
        return await self.read_present_value(self.target_ip,inst)
    
    async def get_return_air_temperature(self, oUnitIndex, iUnitIndex):
        """還空気の温度[C]を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            list(bool,float): 読み取り成功の真偽,還空気の温度[C]
        """
        inst = 'analogInput:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.MeasuredRoomTemperature.value)
        return await self.read_present_value(self.target_ip,inst)
    
    async def get_return_air_relative_humidity(self, oUnitIndex, iUnitIndex):
        """還空気の相対湿度[%]を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            list(bool,float): 読み取り成功の真偽,相対湿度[%]
        """
        inst = 'analogInput:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.MeasuredRelativeHumidity.value)
        return await self.read_present_value(self.target_ip,inst)

# endregion

# region 風量関連

    async def change_fan_speed(self, oUnitIndex, iUnitIndex, speed):
        """ファン風量を変える
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
            speed (FanSpeed): ファン風量
        Returns:
            bool:命令が成功したか否か
        """        
        inst = 'multiStateOutput:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.FanSpeed_Setting.value)
        return await self.write_present_value(self.target_ip,inst,Unsigned(speed.value))
    
    async def get_fan_speed(self, oUnitIndex, iUnitIndex):
        """ファン風量を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            list(bool,FanSpeed): 読み取り成功の真偽,ファン風量
        """        
        inst = 'multiStateInput:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.FanSpeed_Status.value)
        val = await self.read_present_value(self.target_ip,inst)

        return val[0], self.FanSpeed.Low if val[1] == 1 else (self.FanSpeed.Middle if val[1] == 2 else self.FanSpeed.High)

# endregion

# region 風向関連

    async def change_direction(self, oUnitIndex, iUnitIndex, direction):
        """風向を変える
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
            mode (Direction): 風向
        Returns:
            bool:命令が成功したか否か
        """        
        inst = 'multiStateOutput:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.AirflowDirection_Setting.value)
        return await self.write_present_value(self.target_ip,inst,Unsigned(direction.value))
    
    async def get_direction(self, oUnitIndex, iUnitIndex):
        """風向を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            list(bool,Direction): 読み取り成功の真偽,風向
        """        
        inst = 'multiStateInput:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.AirflowDirection_Status.value)
        val = await self.read_present_value(self.target_ip,inst,Unsigned)

        if(val[1] == 1):
            return val[0], self.Direction.Horizontal
        elif(val[1] == 2):
            return val[0], self.Direction.Degree_225
        elif(val[1] == 3):
            return val[0], self.Direction.Degree_450
        elif(val[1] == 4):
            return val[0], self.Direction.Degree_675
        else:
            return val[0], self.Direction.Vertical

# endregion

# region 手元リモコン関連

    async def permit_local_control(self, oUnitIndex, iUnitIndex):
        """手元リモコン操作を許可する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            bool:命令が成功したか否か
        """        
        inst = 'binaryValue:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.RemoteControllerPermittion_Setpoint_Setting.value)
        return await self.write_present_value(self.target_ip,inst,Enumerated(1))

    async def prohibit_local_control(self, oUnitIndex, iUnitIndex):
        """手元リモコン操作を禁止する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            bool:命令が成功したか否か
        """        
        inst = 'binaryValue:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.RemoteControllerPermittion_Setpoint_Setting.value)
        return await self.write_present_value(self.target_ip,inst,Enumerated(0))

    async def is_local_control_permitted(self, oUnitIndex, iUnitIndex):
        """手元リモコン操作が許可されているか否か
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            list(bool,bool): 読み取り成功の真偽,手元リモコン操作が許可されているか否か
        """
        inst = 'binaryInput:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.RemoteControllerPermittion_Setpoint_Status.value)
        val = await self.read_present_value(self.target_ip,inst)
        return val[0], (val[1] == 1)

# endregion

# region 冷媒温度強制制御関連

    async def enable_refrigerant_temperatureControl(self, oUnitIndex):
        """冷媒温度強制制御を有効にする
        Args:
            oUnitIndex (int): 室外機番号（1～4）
        Returns:
            bool:命令が成功したか否か
        """        
        inst = 'binaryValue:' + self._get_ou_objNum(oUnitIndex,self._member.ForcedRefrigerantTemperature_Setting.value)
        return await self.write_present_value(self.target_ip,inst,Enumerated(1))

    async def disable_refrigerant_temperatureControl(self, oUnitIndex):
        """冷媒温度強制制御を無効にする
        Args:
            oUnitIndex (int): 室外機番号（1～4）
        Returns:
            bool:命令が成功したか否か
        """        
        inst = 'binaryValue:' + self._get_ou_objNum(oUnitIndex,self._member.ForcedRefrigerantTemperature_Setting.value)
        return await self.write_present_value(self.target_ip,inst,Enumerated(0))

    async def is_refrigerant_temperature_control_enabled(self, oUnitIndex):
        """冷媒温度強制制御が有効か否かを取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
        Returns:
            list(bool,bool): 読み取り成功の真偽,冷媒温度強制制御が有効か否か
        """
        inst = 'binaryInput:' + self._get_ou_objNum(oUnitIndex,self._member.ForcedRefrigerantTemperature_Status.value)
        val = await self.read_present_value(self.target_ip,inst)
        return val[0], (val[1] == 1)

# endregion

# region 蒸発/凝縮温度関連

    async def change_evaporating_temperature(self, oUnitIndex, evaporatingTemperature):
        """蒸発温度設定値[C]を変える
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            evaporatingTemperature (float): 蒸発温度設定値[C]
        Returns:
        bool:命令が成功したか否か
        """
        inst = 'analogValue:' + self._get_ou_objNum(oUnitIndex,self._member.EvaporatingTemperatureSetpoint_Setting.value)
        return await self.write_present_value(self.target_ip,inst,Real(evaporatingTemperature))
    
    async def get_evaporating_temperature(self, oUnitIndex):
        """蒸発温度設定値[C]を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
        Returns:
            list(bool,float): 読み取り成功の真偽,蒸発温度設定値[C]
        """
        inst = 'analogInput:' + self._get_ou_objNum(oUnitIndex,self._member.EvaporatingTemperatureSetpoint_Status.value)
        return await self.read_present_value(self.target_ip,inst)
    
    async def change_condensing_temperature(self, oUnitIndex, condensingTemperature):
        """凝縮温度設定値[C]を変える
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            condensingTemperature (float): 凝縮温度設定値[C]
        Returns:
            bool:命令が成功したか否か
            """
        inst = 'analogValue:' + self._get_ou_objNum(oUnitIndex,self._member.CondensingTemperatureSetpoint_Setting.value)
        return await self.write_present_value(self.target_ip,inst,Real(condensingTemperature))
    
    async def get_condensing_temperature(self, oUnitIndex):
        """凝縮温度設定値[C]を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
        Returns:
            list(bool,float): 読み取り成功の真偽,凝縮温度設定値[C]
        """
        inst = 'analogInput:' + self._get_ou_objNum(oUnitIndex,self._member.CondensingTemperatureSetpoint_Status.value)
        return await self.read_present_value(self.target_ip,inst)

# endregion

# region 熱負荷

    async def get_indoor_unit_heatload(self, oUnitIndex, iUnitIndex):
        """室内機の熱負荷[kW]を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            list(bool,float): 読み取り成功の真偽,室内機の熱負荷[kW]
        """
        inst = 'analogInput:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.HeatLoad.value)
        return await self.read_present_value(self.target_ip,inst)
    
    async def get_outdoor_unit_heatload(self, oUnitIndex):
        """室外機の熱負荷[kW]を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
        Returns:
            list(bool,float): 読み取り成功の真偽,室外機の熱負荷[kW]
        """
        inst = 'analogInput:' + self._get_ou_objNum(oUnitIndex,self._member.HeatLoad.value)
        return await self.read_present_value(self.target_ip,inst)

# endregion

# region 電力消費関連

    async def get_indoor_unit_electricity(self, oUnitIndex, iUnitIndex):
        """室内機の消費電力[kW]を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            list(bool,float): 読み取り成功の真偽,室内機の消費電力[kW]
        """
        inst = 'analogInput:' + self._get_iu_objNum(oUnitIndex,iUnitIndex,self._member.Electricity.value)
        return await self.read_present_value(self.target_ip,inst)
    
    async def get_outdoor_unit_electricity(self, oUnitIndex):
        """室外機の消費電力[kW]を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
        Returns:
            list(bool,float): 読み取り成功の真偽,室外機の消費電力[kW]
        """
        inst = 'analogInput:' + self._get_ou_objNum(oUnitIndex,self._member.Electricity.value)
        return await self.read_present_value(self.target_ip,inst)

# endregion

# region 補助メソッド

    def _get_iu_objNum(self,oUnitIndex,iUnitIndex,mem_id):
        return str(1000 * oUnitIndex + 100 * iUnitIndex + mem_id)
    
    def _get_ou_objNum(self,oUnitIndex,mem_id):
       return str(1000 * oUnitIndex + mem_id)

# endregion

# region サンプル

async def main():
    vrfCom = VRFSystemCommunicator(12)

    # 起動
    await turn_on(vrfCom)
    
    # 停止
    await turn_off(vrfCom)

    # 熱負荷と電力
    await read_state(vrfCom)
    
    # 無限ループで待機
    while True:
        pass

async def turn_off(vrfCom):
    # 各系統の室内機の台数
    i_unit_num = [5,4,5,4]

    for i in range(len(i_unit_num)):
        for j in range(i_unit_num[i]):
            print('vrf' + str(i + 1) + '-' + str(j+1))

            print('turn off...',end='')
            rslt = await vrfCom.turn_off(i+1,j+1)
            print('success' if rslt[0] else 'failed')

async def turn_on(vrfCom):
    # 各系統の室内機の台数
    i_unit_num = [5,4,5,4]

    for i in range(len(i_unit_num)):
        for j in range(i_unit_num[i]):
            print('vrf' + str(i + 1) + '-' + str(j+1))

            print('turn on...',end='')
            rslt = await vrfCom.turn_on(i+1,j+1)
            print('success' if rslt[0] else 'failed')

            print('change mode...',end='')
            rslt = await vrfCom.change_mode(i+1,j+1,VRFSystemCommunicator.Mode.Cooling)
            print('success' if rslt[0] else 'failed')

            print('change set point temperature...',end='')
            rslt = await vrfCom.change_setpoint_temperature(i+1,j+1,26)
            print('success' if rslt[0] else 'failed')

            print('change fanspeed...',end='')
            rslt = await vrfCom.change_fan_speed(i+1,j+1,VRFSystemCommunicator.FanSpeed.Middle)
            print('success' if rslt[0] else 'failed')

            print('change direction...',end='')
            rslt = await vrfCom.change_direction(i+1,j+1,VRFSystemCommunicator.Direction.Degree_450)
            print('success' if rslt[0] else 'failed')

async def read_state(vrfCom):
    for i in range(4):
        val = await vrfCom.get_outdoor_unit_heatload(i + 1)
        print('Heat load of vrf' + str(i + 1) + (' = ' + '{:.1f}kW'.format(val[1]) if val[0] else ' 通信失敗'))

        val = await vrfCom.get_outdoor_unit_electricity(i + 1)
        print('Electricity of vrf' + str(i + 1) + (' = ' + '{:.1f}kW'.format(val[1]) if val[0] else ' 通信失敗'))


if __name__ == "__main__":
    asyncio.run(main())

# endregion