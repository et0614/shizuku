import PresentValueReadWriter
import time

from enum import Enum
from bacpypes.primitivedata import Real, Integer, Enumerated

class OccupantCommunicator():

# region 定数宣言

    OCCUPANTMONITOR_DEVICE_ID = 5

    OCCUPANTMONITOR_EXCLUSIVE_PORT = 0xBAC0 + OCCUPANTMONITOR_DEVICE_ID

# endregion

# region 列挙型定義

    class _member(Enum):
        # 執務者の数
        OccupantNumber = 1
        # 在室状況
        Availability = 2
        # 温冷感
        ThermalSensation = 3
        # 着衣量
        ClothingIndex = 4

    class Tenant(Enum):
        # 南テナント
        South = 1
        # 北テナント
        North = 2

    class ThermalSensation(Enum):
        # Cold
        Cold = -3
        # Cool
        Cool = -2
        # Slightly Cool
        SlightlyCool = -1
        # Neutral
        Neutral = 0
        # Slightly Warm
        SlightlyWarm = 1
        # Warm
        Warm = 2
        # Hot
        Hot = 3

# endregion

# region コンストラクタ他

    def __init__(self, id, name='occComm', target_ip='127.0.0.1', time_out_sec=1.0):
        """インスタンスを初期化する

        Args:
            id (int): 通信用のDeviceのID
            name (str): 通信用のDeviceの名前
            ip_address (str): Occupant MonitorのIP Address（xxx.xxx.xxx.xxx:port）
        """
        self.target_ip = target_ip + ':' + str(self.OCCUPANTMONITOR_EXCLUSIVE_PORT)
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

    def get_occupant_number(self, tenant):
        """在室している執務者数を取得する
        Args:
            tenant (Tenant): テナント
        Returns:
            list: 読み取り成功の真偽,在室している執務者数
        """
        inst = 'analogInput:' + str(10000 * int(tenant.value) + self._member.OccupantNumber.value)       
        return self.comm.read_present_value(self.target_ip,inst,Integer)
    

    def is_occupant_stay_in_office(self, tenant, occupant_index):
        """在室しているか否かを取得する
        Args:
            tenant (Tenant): テナント
            occupant_index (Unsigned): 執務者番号（1～）
        Returns:
            list(bool,bool): 読み取り成功の真偽,在室しているか否か
        """
        inst = 'binaryInput:' + str(10000 * int(tenant.value) + 100 * occupant_index + self._member.Availability.value)
        val = self.comm.read_present_value(self.target_ip,inst,Enumerated)
        return val[0], (val[1] == 1)


    def get_thermal_sensation(self, tenant, occupant_index):
        """温冷感を取得する
        Args:
            tenant (Tenant): テナント
            occupant_index (Unsigned): 執務者番号（1～）
        Returns:
            list: 読み取り成功の真偽,温冷感
        """        
        inst = 'analogInput:' + str(10000 * int(tenant.value) + 100 * occupant_index + self._member.ThermalSensation.value)
        return self.comm.read_present_value(self.target_ip,inst,Integer)


    def get_clothing_index(self, tenant, occupant_index):
        """着衣量を取得する
        Args:
            tenant (Tenant): テナント
            occupant_index (Unsigned): 執務者番号（1～）
        Returns:
            list: 読み取り成功の真偽,着衣量
        """        
        inst = 'analogInput:' + str(10000 * int(tenant.value) + 100 * occupant_index + self._member.ClothingIndex.value)
        return self.comm.read_present_value(self.target_ip,inst,Real)


def main():
    oCom = OccupantCommunicator(1000)

    while True:
        val = oCom.get_occupant_number(OccupantCommunicator.Tenant.South)
        print('Occupant in south tenant' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))

        val = oCom.get_occupant_number(OccupantCommunicator.Tenant.North)
        print('Occupant in north tenant' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))

        val = oCom.is_occupant_stay_in_office(OccupantCommunicator.Tenant.South, 1)
        print('Occupant 1 stay in office' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))

        val = oCom.get_thermal_sensation(OccupantCommunicator.Tenant.South, 1)
        print('Thermal sensation of occupant 1 ' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))

        val = oCom.get_clothing_index(OccupantCommunicator.Tenant.South, 1)
        print('Clothing index of occupant 1 ' + (' = ' + '{:.1f}'.format(val[1]) if val[0] else ' 通信失敗'))

        time.sleep(1)

if __name__ == "__main__":
    main()