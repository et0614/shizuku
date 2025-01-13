import datetime
import asyncio
import PresentValueReadWriter
from enum import Enum

from bacpypes3.pdu import Address
from bacpypes3.primitivedata import ObjectIdentifier, Real

class DateTimeCommunicator(PresentValueReadWriter.PresentValueReadWriter):
    """Shizuku2のDateTimeControllerとの通信ユーティリティクラス
    """  

    # region 定数宣言

    DATETIMECONTROLLER_DEVICE_ID = 1

    DATETIMECONTROLLER_EXCLUSIVE_PORT = 0xBAC0 + DATETIMECONTROLLER_DEVICE_ID

    # endregion

    # region 列挙型定義

    class _member(Enum):
        # 現在のシミュレーション上の日時
        CurrentDateTimeInSimulation = 1
        # 加速度
        AccelerationRate = 2
        # 現実時間の基準日時
        BaseRealDateTime = 3
        # シミュレーション上の基準日時
        BaseAcceleratedDateTime = 4
        # シミュレーション上の終了日時
        EndDateTime = 5
        # 計算遅延中か否か
        IsDelayed = 6
        # 計算完了済か否か
        IsFinished = 7

    # endregion

    # region 初期化処理

    def __init__(self, id, name='dtComm', device_ip='127.0.0.1', emulator_ip='127.0.0.1', time_out_sec = 1.0):
        """インスタンスを初期化する

        Args:
            id (int): 通信に使うDeviceのID
            name (str): 通信に使うDeviceの名前
            device_ip (str): 通信に使うDeviceのIP Address（xxx.xxx.xxx.xxx）
            emulator_ip (str): エミュレータのIP Address（xxx.xxx.xxx.xxx）
            time_out_sec (float): タイムアウトまでの時間[sec]
        """
        super().__init__(id, name, device_ip, emulator_ip, time_out_sec)
        self.__target_ip = emulator_ip + ':' + str(self.DATETIMECONTROLLER_EXCLUSIVE_PORT)

        # DateTimeのCOV登録状況
        self.dtcov_scribed = False
        self.__acc_rate = 0
        self.__base_real_datetime = datetime.datetime.today()
        self.__base_sim_datetime = datetime.datetime.today()

    # endregion

    # region 現在日時取得関連

    async def subscribe_date_time_cov(self):
        """シミュレーション日時に関する情報をCOVを登録する
        Args:None
        Returns:None
        """        
        asyncio.create_task(self.cov_loop())

    async def cov_loop(self):
        # 既に登録されている場合には日時だけ更新して二重登録を回避
        if self.dtcov_scribed:
            return await self.sync_date_time()
        try:
            async with self.bacdevice.change_of_value(
                address=Address(self.__target_ip),
                subscriber_process_identifier=self.id,
                monitored_object_identifier=ObjectIdentifier('analogOutput:' + str(DateTimeCommunicator._member.AccelerationRate.value)), # 加速度
                lifetime=60*60*24*365, #1年間
                issue_confirmed_notifications=True
            ) as scm: #SubscriptionContextManager
                while True:
                    property_identifier, property_value = await scm.get_value()
                    if(f"{property_identifier}"=='present-value'):
                        self.dtcov_scribed = True
                        await self.sync_date_time()
        except Exception as err:
            return False

    async def sync_date_time(self):
        """日時をエミュレータに同期させる
        Args:
        Returns:
            bool: 成功したか否か
        """ 
        val = await self.get_acceleration_rate()
        if val[0]: self.__acc_rate = val[1]
        else: return False 
        val = await self.read_present_value(self.__target_ip, 'datetimeValue:' + str(DateTimeCommunicator._member.BaseRealDateTime.value))
        if val[0]: self.__base_real_datetime = val[1]
        else: return False
        val = await self.read_present_value(self.__target_ip, 'datetimeValue:' + str(DateTimeCommunicator._member.BaseAcceleratedDateTime.value))
        if val[0]: self.__base_sim_datetime = val[1]
        else: return False
        return True

    def current_date_time(self):
        """現在の日時を取得する
        Args:
        Returns:
            datetime: 現在の日時
        """        
        return (datetime.datetime.today() - self.__base_real_datetime) * self.__acc_rate + self.__base_sim_datetime

    # endregion

    # region 加速度関連

    async def get_acceleration_rate(self):
        """加速度[-]を取得する

        Returns:
            list: 読み取り成功の真偽,加速度[-]
        """
        return await self.read_present_value(self.__target_ip,'analogOutput:' + str(DateTimeCommunicator._member.AccelerationRate.value))

    async def change_acceleration_rate(self, acceleration_rate):
        """加速度[-]を変える
        Args:
            acceleration_rate (float): 加速度[-]
        Returns:
            bool:命令が成功したか否か
            """
        return await self.write_present_value(self.__target_ip,'analogOutput:' + str(DateTimeCommunicator._member.AccelerationRate.value),Real(acceleration_rate))

    # endregion

    # region その他の処理

    async def get_end_dateTime(self):
        """計算を終えるシミュレーション上の日時を取得する

        Returns:
            list: 読み取り成功の真偽,計算を終えるシミュレーション上の日時
        """
        return await self.read_present_value(self.__target_ip,'datetime-value:' + str(DateTimeCommunicator._member.EndDateTime.value))


    async def get_is_emulator_delayed(self):
        """計算遅延の有無を取得するを取得する

        Returns:
            list: 読み取り成功の真偽,計算遅延の有無を取得する
        """
        val = await self.read_present_value(self.__target_ip,'binary-input:' + str(DateTimeCommunicator._member.IsDelayed.value))
        return val[0], (val[1] == 1)
    

    async def get_is_emulator_finished(self):
        """計算が終了済か否かを取得する

        Returns:
            list: 読み取り成功の真偽,計算が終了済か否か
        """
        val = await self.read_present_value(self.__target_ip,'binary-input:' + str(DateTimeCommunicator._member.IsFinished.value))
        return val[0], (val[1] == 1)

    # endregion

# region サンプル

async def main():
    pv_rw = DateTimeCommunicator(
        id=50,
        name= 'myDevice',
        device_ip='127.0.0.1/255.255.255.0',
        emulator_ip='127.0.0.1',
        time_out_sec=1
    )

    # 日時のCOVを登録
    print('Subscribe COV')
    await pv_rw.subscribe_date_time_cov()
    await pv_rw.sync_date_time()

    # 無限ループで日時を表示
    while True:
        print(pv_rw.current_date_time().strftime('%Y/%m/%d %H:%M:%S'))
        val = await pv_rw.get_is_emulator_delayed()
        print('Is delayed? ' + (str(val[1]) if val[0] else ' 通信失敗'))
        val = await pv_rw.get_is_emulator_finished()
        print('Is finished? ' + (str(val[1]) if val[0] else ' 通信失敗'))
        print()
        await asyncio.sleep(1)
        pass

if __name__ == "__main__":
    asyncio.run(main())

# endregion