import PresentValueReadWriter
import time
import asyncio

from enum import Enum

class OccupantCommunicator(PresentValueReadWriter.PresentValueReadWriter):

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
        # 熱的な不満足者率
        Dissatisfied_Thermal = 5
        # ドラフトによる不満足者率
        Dissatisfied_Draft = 6
        # 上下温度分布による不満足者率
        Dissatisfied_VerticalTemp = 7

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

# region コンストラクタ

    def __init__(self, id, name='occComm', device_ip='127.0.0.1', emulator_ip='127.0.0.1', time_out_sec=1.0):
        """インスタンスを初期化する

        Args:
            id (int): 通信用のDeviceのID
            name (str): 通信用のDeviceの名前
            device_ip (str): 通信に使うDeviceのIP Address（xxx.xxx.xxx.xxx）
            emulator_ip (str): エミュレータのIP Address（xxx.xxx.xxx.xxx）
        """
        super().__init__(id, name, device_ip, emulator_ip, time_out_sec)
        self.target_ip = emulator_ip + ':' + str(self.OCCUPANTMONITOR_EXCLUSIVE_PORT)

# endregion

# region テナント・ゾーン別

    async def get_occupant_number(self, tenant):
        """在室している執務者数を取得する
        Args:
            tenant (Tenant): テナント
        Returns:
            list: 読み取り成功の真偽,在室している執務者数
        """
        inst = 'analogInput:' + str(10000 * int(tenant.value) + self._member.OccupantNumber.value)       
        return await self.read_present_value(self.target_ip,inst)


    async def get_zone_occupant_number(self, tenant, zone_number):
        """ゾーンに在室している執務者数を取得する
        Args:
            tenant (Tenant): テナント
            zone_number (Unsigned): ゾーン番号
        Returns:
            list: 読み取り成功の真偽,ゾーンに在室している執務者数
        """
        inst = 'analogInput:' + str(10000 * int(tenant.value) + 1000 * zone_number + self._member.OccupantNumber.value)       
        return await self.read_present_value(self.target_ip,inst)


    async def get_averaged_thermal_sensation(self, tenant, zone_number):
        """ゾーンに在室している執務者数の平均温冷感を取得する
        Args:
            tenant (Tenant): テナント
            zone_number (Unsigned): ゾーン番号（1~9）
        Returns:
            list: 読み取り成功の真偽,平均温冷感
        """        
        inst = 'analogInput:' + str(10000 * int(tenant.value) + 1000 * zone_number + self._member.ThermalSensation.value)
        return await self.read_present_value(self.target_ip,inst)
    

    async def get_averaged_clothing_index(self, tenant, zone_number):
        """ゾーンに在室している執務者数の平均着衣量を取得する
        Args:
            tenant (Tenant): テナント
            zone_number (Unsigned): ゾーン番号（1~9）
        Returns:
            list: 読み取り成功の真偽,平均着衣量
        """        
        inst = 'analogInput:' + str(10000 * int(tenant.value) + 1000 * zone_number + self._member.ClothingIndex.value)
        return await self.read_present_value(self.target_ip,inst)
    
    async def get_thermally_dissatisfied_rate(self, tenant, zone_number):
        """ゾーンに在室している執務者の温熱環境に対する不満足者率を取得する
        Args:
            tenant (Tenant): テナント
            zone_number (Unsigned): ゾーン番号（1~9）
        Returns:
            list: 読み取り成功の真偽,ゾーンに在室している執務者の温熱環境に対する不満足者率
        """        
        inst = 'analogInput:' + str(10000 * int(tenant.value) + 1000 * zone_number + self._member.Dissatisfied_Thermal.value)
        return await self.read_present_value(self.target_ip,inst)
    
    async def get_dissatisfied_rate_caused_by_draft(self, tenant, zone_number):
        """ゾーンに在室している執務者のドラフトに対する不満足者率を取得する
        Args:
            tenant (Tenant): テナント
            zone_number (Unsigned): ゾーン番号（1~9）
        Returns:
            list: 読み取り成功の真偽,ゾーンに在室している執務者のドラフトに対する不満足者率
        """        
        inst = 'analogInput:' + str(10000 * int(tenant.value) + 1000 * zone_number + self._member.Dissatisfied_Draft.value)
        return await self.read_present_value(self.target_ip,inst)
    
    async def get_dissatisfied_rate_caused_by_vertical_temperature_distribution(self, tenant, zone_number):
        """ゾーンに在室している執務者の上下温度分布に対する不満足者率を取得する
        Args:
            tenant (Tenant): テナント
            zone_number (Unsigned): ゾーン番号（1~9）
        Returns:
            list: 読み取り成功の真偽,ゾーンに在室している執務者の上下温度分布に対する不満足者率
        """        
        inst = 'analogInput:' + str(10000 * int(tenant.value) + 1000 * zone_number + self._member.Dissatisfied_VerticalTemp.value)
        return await self.read_present_value(self.target_ip,inst)

# endregion

# region 執務者別    

    async def is_occupant_stay_in_office(self, tenant, occupant_index):
        """在室しているか否かを取得する
        Args:
            tenant (Tenant): テナント
            occupant_index (Unsigned): 執務者番号（1～）
        Returns:
            list(bool,bool): 読み取り成功の真偽,在室しているか否か
        """
        inst = 'binary-input,' + str(10000 * int(tenant.value) + 10 * occupant_index + self._member.Availability.value)
        val = await self.read_present_value(self.target_ip,inst)
        return val[0], (val[1] == 1)


    async def get_thermal_sensation(self, tenant, occupant_index):
        """温冷感を取得する
        Args:
            tenant (Tenant): テナント
            occupant_index (Unsigned): 執務者番号（1～）
        Returns:
            list: 読み取り成功の真偽,温冷感
        """        
        inst = 'analogInput:' + str(10000 * int(tenant.value) + 10 * occupant_index + self._member.ThermalSensation.value)
        return await self.read_present_value(self.target_ip,inst)


    async def get_clothing_index(self, tenant, occupant_index):
        """着衣量を取得する
        Args:
            tenant (Tenant): テナント
            occupant_index (Unsigned): 執務者番号（1～）
        Returns:
            list: 読み取り成功の真偽,着衣量
        """        
        inst = 'analogInput:' + str(10000 * int(tenant.value) + 10 * occupant_index + self._member.ClothingIndex.value)
        return await self.read_present_value(self.target_ip,inst)

# endregion

async def main():
    oCom = OccupantCommunicator(15)

    while True:
        val = await oCom.get_occupant_number(OccupantCommunicator.Tenant.South)
        print('Occupant in south tenant' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))

        val = await oCom.get_occupant_number(OccupantCommunicator.Tenant.North)
        print('Occupant in north tenant' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))

        val = await oCom.is_occupant_stay_in_office(OccupantCommunicator.Tenant.South, 1)
        print('Occupant 1 stay in office' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))

        val = await oCom.get_thermal_sensation(OccupantCommunicator.Tenant.South, 1)
        print('Thermal sensation of occupant 1 ' + (' = ' + str(val[1]) if val[0] else ' 通信失敗'))

        val = await oCom.get_clothing_index(OccupantCommunicator.Tenant.South, 1)
        print('Clothing index of occupant 1 ' + (' = ' + '{:.1f}'.format(val[1]) if val[0] else ' 通信失敗'))

        time.sleep(1)

if __name__ == "__main__":
    asyncio.run(main())