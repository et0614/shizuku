import PresentValueReadWriter
import time

from enum import Enum
from bacpypes.primitivedata import Real, Unsigned, Enumerated

class VentilationSystemCommunicator():

# region 定数宣言

    VENTCTRL_DEVICE_ID = 5

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

# region コンストラクタ他

    def __init__(self, id, name='envComm', target_ip='127.0.0.1', time_out_sec=1.0):
        """インスタンスを初期化する

        Args:
            id (int): 通信用のDeviceのID
            name (str): 通信用のDeviceの名前
            ip_address (str): Ventilation ControllerのIP Address（xxx.xxx.xxx.xxx:port）
        """
        self.target_ip = target_ip + ':' + str(self.VENTCTRL_EXCLUSIVE_PORT)
        self.comm = PresentValueReadWriter.PresentValueReadWriter(id,name,time_out_sec)

    def subscribe_date_time_cov(self, monitored_ip):
        """シミュレーション日時の加速度に関するCOVを登録する

        Args:
            monitored_ip (str): DateTimeControllerオブジェクトのIPアドレス(xxx.xxx.xxx.xxx:xxxxの形式)

        Returns:
            bool: 登録が成功したか否か
        """
        return self.comm.subscribe_date_time_cov(monitored_ip)
    
    def current_date_time(self):
        """現在の日時を取得する

        Returns:
            datetime: 現在の日時
        """        
        return self.comm.current_date_time()

# endregion

# region テナント別の処理

    def get_south_tenant_CO2_level(self):
        """南側テナントのCO2濃度[ppm]を取得する
        Returns:
            list: 読み取り成功の真偽,南側テナントのCO2濃度[ppm]
        """        
        inst = 'analogInput:' + str(self._member.SouthCO2Level.value)
        return self.comm.read_present_value(self.target_ip,inst,Real)
    

    def get_north_tenant_CO2_level(self):
        """北側テナントのCO2濃度[ppm]を取得する
        Returns:
            list: 読み取り成功の真偽,北側テナントのCO2濃度[ppm]
        """        
        inst = 'analogInput:' + str(self._member.NorthCO2Level.value)
        return self.comm.read_present_value(self.target_ip,inst,Real)

# endregion    

# region Hex

    def start_ventilation(self, oUnitIndex, iUnitIndex, comAsync=False):
        """換気（全熱交換器）を起動する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
            comAsync(bool):非同期で命令するか否か
        Returns:
            bool:命令が成功したか否か（非同期の場合には常にFalse）
        """        
        inst = 'binaryOutput:' + self._get_instance_number(oUnitIndex,iUnitIndex,self._member.HexOnOff.value)
        if(comAsync):
            self.comm.write_present_value_async(self.target_ip,inst,Enumerated(1), None)
            return False
        else:
            return self.comm.write_present_value(self.target_ip,inst,Enumerated(1))


    def stop_ventilation(self, oUnitIndex, iUnitIndex, comAsync=False):
        """換気（全熱交換器）を停止する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
            comAsync(bool):非同期で命令するか否か
        Returns:
            bool:命令が成功したか否か（非同期の場合には常にFalse）
        """        
        inst = 'binaryOutput:' + self._get_instance_number(oUnitIndex,iUnitIndex,self._member.HexOnOff.value)
        if(comAsync):
            self.comm.write_present_value_async(self.target_ip,inst,Enumerated(0), None)
            return False
        else:
            return self.comm.write_present_value(self.target_ip,inst,Enumerated(0))


    def enable_bypass_control(self, oUnitIndex, iUnitIndex, comAsync=False):
        """バイパス制御を有効にする
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
            comAsync(bool):非同期で命令するか否か
        Returns:
            bool:命令が成功したか否か（非同期の場合には常にFalse）
        """        
        inst = 'binaryOutput:' + self._get_instance_number(oUnitIndex,iUnitIndex,self._member.HexBypassEnabled.value)
        if(comAsync):
            self.comm.write_present_value_async(self.target_ip,inst,Enumerated(1), None)
            return False
        else:
            return self.comm.write_present_value(self.target_ip,inst,Enumerated(1))


    def disable_bypass_control(self, oUnitIndex, iUnitIndex, comAsync=False):
        """バイパス制御を無効にする
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
            comAsync(bool):非同期で命令するか否か
        Returns:
            bool:命令が成功したか否か（非同期の場合には常にFalse）
        """        
        inst = 'binaryOutput:' + self._get_instance_number(oUnitIndex,iUnitIndex,self._member.HexBypassEnabled.value)
        if(comAsync):
            self.comm.write_present_value_async(self.target_ip,inst,Enumerated(0), None)
            return False
        else:
            return self.comm.write_present_value(self.target_ip,inst,Enumerated(0))


    def change_fan_speed(self, oUnitIndex, iUnitIndex, speed, comAsync=False):
        """ファン風量を変える
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
            speed (FanSpeed): ファン風量
            comAsync(bool):非同期で命令するか否か
        Returns:
            bool:命令が成功したか否か
        """        
        inst = 'multiStateOutput:' + self._get_instance_number(oUnitIndex,iUnitIndex,self._member.HexFanSpeed.value)
        if(comAsync):
            self.comm.write_present_value_async(self.target_ip,inst,Unsigned(speed.value),None)
            return False
        else:
            return self.comm.write_present_value(self.target_ip,inst,Unsigned(speed.value))
    

    def get_fan_speed(self, oUnitIndex, iUnitIndex):
        """ファン風量を取得する
        Args:
            oUnitIndex (int): 室外機番号（1～4）
            iUnitIndex (int): 室内機番号（1～5）
        Returns:
            list(bool,FanSpeed): 読み取り成功の真偽,ファン風量
        """        
        inst = 'multiStateInput:' + self._get_instance_number(oUnitIndex,iUnitIndex,self._member.HexFanSpeed.value)
        val = self.comm.read_present_value(self.target_ip,inst,Unsigned)

        return val[0], self.FanSpeed.Low if val[1] == 1 else (self.FanSpeed.Middle if val[1] == 2 else self.FanSpeed.High)

# endregion

# region 補助メソッド

    def _get_instance_number(self,oUnitIndex,iUnitIndex,mem_id):
        return str(1000 * oUnitIndex + 100 * iUnitIndex + mem_id)

# endregion


def main():
    vCom = VentilationSystemCommunicator(1000)

    while True:
        val = vCom.get_south_tenant_CO2_level()
        print('CO2 level of south tenant' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))
        
        val = vCom.get_north_tenant_CO2_level()
        print('CO2 level of north tenant' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))

        
        vCom.start_ventilation(1,1)
        print('Occupant in north tenant' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))

        val = vCom.is_occupant_stay_in_office(OccupantCommunicator.Tenant.South, 1)
        print('Occupant 1 stay in office' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))

        val = vCom.get_thermal_sensation(OccupantCommunicator.Tenant.South, 1)
        print('Thermal sensation of occupant 1 ' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))

        time.sleep(1)

if __name__ == "__main__":
    main()