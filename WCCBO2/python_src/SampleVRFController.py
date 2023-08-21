import time, datetime
import VRFCommunicator

def main():
    vrfCom = VRFCommunicator.VRFCommunicator(999)

    # 現在時刻の加速度のCOVイベントへ登録する（current_date_timeが有効になる）
    print('Subscribe COV...')
    while not vrfCom.subscribe_date_time_cov():
        time.sleep(0.1)
    print('success')

    # 各系統の室内機の台数
    i_unit_num = [6,6,6,8]

    last_dt = vrfCom.current_date_time()
    while True:
        # 現在のシミュレーション内の日時を表示
        dt = vrfCom.current_date_time()
        print(dt.strftime('%Y/%m/%d %H:%M:%S'))

        # 空調モード判定
        cmode = is_cooling(dt)

        # 空調開始時
        if(not(is_hvac_time(last_dt)) and is_hvac_time(dt)):
            for i in range(len(i_unit_num)):
                for j in range(i_unit_num[i]):
                    print('vrf' + str(i + 1) + '-' + str(j+1))

                    print('turn on...',end='')
                    rslt = vrfCom.turn_on(i+1,j+1)
                    print('success' if rslt[0] else 'failed: ' + rslt[1])

                    print('change mode...',end='')
                    rslt = vrfCom.change_mode(i+1,j+1,VRFCommunicator.VRFCommunicator.Mode.Cooling if cmode else VRFCommunicator.VRFCommunicator.Mode.Heating)
                    print('success' if rslt[0] else 'failed: ' + rslt[1])

                    print('change set point temperature...',end='')
                    rslt = vrfCom.change_setpoint_temperature(i+1,j+1,26 if cmode else 22)
                    print('success' if rslt[0] else 'failed: ' + rslt[1])

                    print('change fanspeed...',end='')
                    rslt = vrfCom.change_fan_speed(i+1,j+1,VRFCommunicator.VRFCommunicator.FanSpeed.Middle)
                    print('success' if rslt[0] else 'failed: ' + rslt[1])

                    print('change direction...',end='')
                    rslt = vrfCom.change_direction(i+1,j+1,VRFCommunicator.VRFCommunicator.Direction.Degree_450)
                    print('success' if rslt[0] else 'failed: ' + rslt[1])

        # 空調停止時
        if(is_hvac_time(last_dt) and not(is_hvac_time(dt))):
            for i in range(len(i_unit_num)):
                for j in range(i_unit_num[i]):
                    print('vrf' + str(i + 1) + '-' + str(j+1))

                    print('turn off...',end='')
                    rslt = vrfCom.turn_off(i+1,j+1)
                    print('success' if rslt else 'failed')

        last_dt = dt # 前回の日時を保存
        time.sleep(0.5)

def is_weekday(dtime):
    dy = dtime.weekday()  
    return not (dy == 5 or dy == 6)    

def is_hvac_time(dtime):
    start_time = datetime.time(7, 0)
    end_time = datetime.time(19, 0)   
    now = dtime.time()    
    return is_weekday(dtime) and (start_time <= now <= end_time)

def is_cooling(dtime):
    return 5 <= dtime.month and dtime.month <= 10

if __name__ == "__main__":
    main()
