from PresentValueReadWriter import PresentValueReadWriter
import asyncio

async def main():
    pv_rw = PresentValueReadWriter(id=10)

    # 日時のCOVを登録
    print('Subscribe COV')
    await pv_rw.subscribe_date_time_cov()

    # 無限ループで日時を表示
    while True:
        dt = pv_rw.current_date_time()
        print(dt.strftime('%Y/%m/%d %H:%M:%S'))
        await asyncio.sleep(1)
        pass

asyncio.run(main())
