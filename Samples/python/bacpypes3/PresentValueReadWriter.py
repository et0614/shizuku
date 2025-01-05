import datetime
import asyncio

from bacpypes3.pdu import Address, IPv4Address
from bacpypes3.ipv4.app import NormalApplication
from bacpypes3.primitivedata import ObjectIdentifier, Enumerated, Real, Integer, Unsigned
from bacpypes3.basetypes import DateTime
from bacpypes3.local.device import DeviceObject
from bacpypes3.apdu import ErrorRejectAbortNack

class PresentValueReadWriter():
    """BACnet通信でPresent valueを読み書きするクラス
    """  

    DATETIMECONTROLLER_DEVICE_ID = 1

    DATETIMECONTROLLER_EXCLUSIVE_PORT = 0xBAC0 + DATETIMECONTROLLER_DEVICE_ID

    def __init__(self, id, name='anonymous device', device_ip='127.0.0.1', emulator_ip='127.0.0.1', time_out_sec = 1.0):
        """インスタンスを初期化する

        Args:
            id (int): 通信に使うDeviceのID
            name (str): 通信に使うDeviceの名前
            device_ip (str): 通信に使うDeviceのIP Address（xxx.xxx.xxx.xxx）
            emulator_ip (str): エミュレータのIP Address（xxx.xxx.xxx.xxx）
            time_out_sec (float): タイムアウトまでの時間[sec]
        """

        # タイムアウトまでの時間
        self.time_out = time_out_sec

        # idを保存
        self.id = id

        # DateTimeControllerのIPアドレスを保存
        self.dtc_id = emulator_ip + ':' + str(self.DATETIMECONTROLLER_EXCLUSIVE_PORT)

        # DateTimeのCOV登録状況
        self.dtcov_scribed = False
        self.acc_rate = 0
        self.base_real_datetime = datetime.datetime.today()
        self.base_sim_datetime = datetime.datetime.today()

        this_device = DeviceObject(
            objectName=name,
            objectIdentifier=id,
            maxApduLengthAccepted=1024,
            segmentationSupported='segmentedBoth',
            vendorIdentifier=15,
        )

        # BACnetコントローラを用意
        ipv4_address = IPv4Address(device_ip, int(0xBAC0 + id))
        self.bacdevice = NormalApplication(this_device, ipv4_address)

# region readproperty関連

    async def read_present_value(self, addr, obj_id):
        """Read property requestでPresent valueを読み取る（同期処理）

        Args:
            addr (string): 通信先のBACnet Deviceのアドレス（xxx.xxx.xxx.xxx:port）
            obj_id (string): 通信先のBACnet DeviceのオブジェクトID

        Returns:
            list: 読み取り成功の真偽, Present value
        """

        try:
            response = await self.bacdevice.read_property(
                address=Address(addr),
                objid=ObjectIdentifier(obj_id),
                prop='present-value'
            )

            if isinstance(response, DateTime):
                return True, datetime.datetime(
                    year=1900 + response.date[0],
                    month=response.date[1],
                    day=response.date[2],
                    hour=response.time[0],
                    minute=response.time[1],
                    second=response.time[2],
                    microsecond=10000 * response.time[3]) # Hundredths (BACnet標準) -> microsecond
            else:
                return True, response
        except ErrorRejectAbortNack as err:
            return False, err

# endregion

# region writeproperty関連

    async def write_present_value(self, addr, obj_id, value):
        """Write property requestでPresent valueを書き込む（同期処理）

        Args:
            addr (string): 通信先のBACnet Deviceのアドレス（xxx.xxx.xxx.xxx:port）
            obj_id (string): 通信先のBACnet DeviceのオブジェクトID
            value (Union[Real,Boolean,Integer,DateTime]): Present value

        Returns:
            bool: 書き込み成功の真偽
        """        

        try:
            await self.bacdevice.write_property(
                address=Address(addr),
                objid=ObjectIdentifier(obj_id),
                prop='present-value',
                value=value
            )
            return True, None
        except ErrorRejectAbortNack as err:
            return False, err

# endregion

# region datetime COV関連

    async def subscribe_date_time_cov(self):
        """シミュレーション日時の加速度に関するCOVを登録する

        Args:
            monitored_ip (str): DateTimeControllerオブジェクトのIPアドレス(xxx.xxx.xxx.xxxの形式)

        Returns:
            None
        """        
        asyncio.create_task(self.cov_loop())

    async def cov_loop(self):
        # 既に登録されている場合には日時だけ更新して二重登録を回避
        if self.dtcov_scribed:
            return await self.sync_date_time()
        try:
            async with self.bacdevice.change_of_value(
                address=Address(self.dtc_id),
                subscriber_process_identifier=self.id,
                monitored_object_identifier=ObjectIdentifier('analog-output:2'), # 加速度
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
        val = await self.read_present_value(self.dtc_id, 'analogOutput:2')
        if val[0]: self.acc_rate = val[1]
        else: return False 
        val = await self.read_present_value(self.dtc_id, 'datetimeValue:3')
        if val[0]: self.base_real_datetime = val[1]
        else: return False
        val = await self.read_present_value(self.dtc_id, 'datetimeValue:4')
        if val[0]: self.base_sim_datetime = val[1]
        else: return False
        return True

    def current_date_time(self):
        """現在の日時を取得する
        Args:
        Returns:
            datetime: 現在の日時
        """        
        return (datetime.datetime.today() - self.base_real_datetime) * self.acc_rate + self.base_sim_datetime

# endregion

# region 加速度関連

    async def get_acceleration_rate(self):
        """加速度[-]を取得する

        Returns:
            list: 読み取り成功の真偽,加速度[-]
        """
        return await self.read_present_value(self.dtc_id,'analogOutput:2')

    async def change_acceleration_rate(self, acceleration_rate):
        """加速度[-]を変える
        Args:
            acceleration_rate (float): 加速度[-]
        Returns:
            bool:命令が成功したか否か
            """
        return await self.write_present_value(self.dtc_id,'analogOutput:2',Real(acceleration_rate))

# endregion

# region サンプル

async def main():
    pv_rw = PresentValueReadWriter(
        id=50,
        name= 'myDevice',
        device_ip='127.0.0.1/255.255.255.0',
        emulator_ip='127.0.0.1',
        time_out_sec=1
    )

    # 日時のCOVを登録
    print('Subscribe COV')
    await pv_rw.subscribe_date_time_cov()

    # read property
    pValue = await pv_rw.read_present_value(addr='127.0.0.1:47817', obj_id='analog-value,4')
    print('Read property ' + ('success, value=' + str(pValue[1]) if pValue[0] else ('failed because of ' + str(pValue[1].reason))))
    pValue = await pv_rw.read_present_value(addr='127.0.0.1:47817', obj_id='analog-output,5')
    print('Read property ' + ('success, value=' + str(pValue[1]) if pValue[0] else ('failed because of ' + str(pValue[1].reason))))
    pValue = await pv_rw.read_present_value(addr='127.0.0.1:47817', obj_id='analog-input,6')
    print('Read property ' + ('success, value=' + str(pValue[1]) if pValue[0] else ('failed because of ' + str(pValue[1].reason))))
    pValue = await pv_rw.read_present_value(addr='127.0.0.1:47817', obj_id='binary-value,7')
    print('Read property ' + ('success, value=' + str(pValue[1]) if pValue[0] else ('failed because of ' + str(pValue[1].reason))))
    pValue = await pv_rw.read_present_value(addr='127.0.0.1:47817', obj_id='binary-output,8')
    print('Read property ' + ('success, value=' + str(pValue[1]) if pValue[0] else ('failed because of ' + str(pValue[1].reason))))
    pValue = await pv_rw.read_present_value(addr='127.0.0.1:47817', obj_id='binary-input,9')
    print('Read property ' + ('success, value=' + str(pValue[1]) if pValue[0] else ('failed because of ' + str(pValue[1].reason))))
    pValue = await pv_rw.read_present_value(addr='127.0.0.1:47817', obj_id='multi-state-value,10')
    print('Read property ' + ('success, value=' + str(pValue[1]) if pValue[0] else ('failed because of ' + str(pValue[1].reason))))
    pValue = await pv_rw.read_present_value(addr='127.0.0.1:47817', obj_id='multi-state-output,11')
    print('Read property ' + ('success, value=' + str(pValue[1]) if pValue[0] else ('failed because of ' + str(pValue[1].reason))))
    pValue = await pv_rw.read_present_value(addr='127.0.0.1:47817', obj_id='multi-state-input,12')
    print('Read property ' + ('success, value=' + str(pValue[1]) if pValue[0] else ('failed because of ' + str(pValue[1].reason))))
    pValue = await pv_rw.read_present_value(addr='127.0.0.1:47817', obj_id='datetime-value,13')
    print('Read property ' + ('success, value=' + str(pValue[1]) if pValue[0] else ('failed because of ' + str(pValue[1].reason))))

    #write property
    success = await pv_rw.write_present_value('127.0.0.1:47817', 'analogValue:1', Integer(3))
    print('Writing analogValue(int) ' + ('success' if success[0] else ('failed because of ' + str(success[1]))))
    success = await pv_rw.write_present_value('127.0.0.1:47817', 'analogOutput:2', Integer(2))
    print('Writing analogOutput(int) ' + ('success' if success[0] else ('failed because of ' + str(success[1]))))
    success = await pv_rw.write_present_value('127.0.0.1:47817', 'analogInput:3', Integer(1))
    print('Writing analogInput(int) ' + ('success' if success[0] else ('failed because of ' + str(success[1]))))
    success = await pv_rw.write_present_value('127.0.0.1:47817', 'analogValue:4', Real(3))
    print('Writing analogValue(real) ' + ('success' if success[0] else ('failed because of ' + str(success[1]))))
    success = await pv_rw.write_present_value('127.0.0.1:47817', 'analogOutput:5', Real(2))
    print('Writing analogOutput(real) ' + ('success' if success[0] else ('failed because of ' + str(success[1]))))
    success = await pv_rw.write_present_value('127.0.0.1:47817', 'analogInput:6', Real(1))
    print('Writing analogInput(real) ' + ('success' if success[0] else ('failed because of ' + str(success[1]))))
    success = await pv_rw.write_present_value('127.0.0.1:47817', 'binaryValue:7', Enumerated(1))
    print('Writing binaryValue ' + ('success' if success[0] else ('failed because of ' + str(success[1]))))
    success = await pv_rw.write_present_value('127.0.0.1:47817', 'binaryOutput:8', Enumerated(1))
    print('Writing binaryOutput ' + ('success' if success[0] else ('failed because of ' + str(success[1]))))
    success = await pv_rw.write_present_value('127.0.0.1:47817', 'binaryInput:9', Enumerated(1))
    print('Writing binaryInput ' + ('success' if success[0] else ('failed because of ' + str(success[1]))))
    success = await pv_rw.write_present_value('127.0.0.1:47817', 'multiStateValue:10', Unsigned(3))
    print('Writing multiStateValue ' + ('success' if success[0] else ('failed because of ' + str(success[1]))))
    success = await pv_rw.write_present_value('127.0.0.1:47817', 'multiStateOutput:11', Unsigned(2))
    print('Writing multiStateOutput ' + ('success' if success[0] else ('failed because of ' + str(success[1]))))
    success = await pv_rw.write_present_value('127.0.0.1:47817', 'multiStateInput:12', Unsigned(1))
    print('Writing multiStateInput ' + ('success' if success[0] else ('failed because of ' + str(success[1]))))

    # 無限ループで日時を表示
    while True:
        print(pv_rw.current_date_time().strftime('%Y/%m/%d %H:%M:%S'))
        await asyncio.sleep(1)
        pass

if __name__ == "__main__":
    asyncio.run(main())
    # main()

# endregion