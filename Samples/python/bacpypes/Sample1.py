import time
import PresentValueReadWriter

pvrw = PresentValueReadWriter.PresentValueReadWriter(10)

# 現在時刻の加速度のCOVイベントへ登録する（current_date_timeが有効になる）
print('Subscribe COV...',end='')
while not pvrw.subscribe_date_time_cov():
    time.sleep(0.1)
print('success')

while True:
    # 現在のシミュレーション内の日時を表示
    dt = pvrw.current_date_time()
    print(dt.strftime('%Y/%m/%d %H:%M:%S'))
    time.sleep(1.0)

