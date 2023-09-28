import time, datetime
import VRFCommunicator as vrc
import VentilationSystemCommunicator as vsc

def main():
    vrCom = vrc.VRFCommunicator(12)
    vsCom = vsc.VentilationSystemCommunicator(16)

    # Enable current_date_time method
    print('Subscribe COV...')
    while not vrCom.subscribe_date_time_cov():
        time.sleep(0.1)
    print('success')

    # Number of indoor units in each VRF system
    i_unit_num = [5,4,5,4]

    last_dt = vrCom.current_date_time()
    while True:
        # Output current date and time
        dt = vrCom.current_date_time()
        print(dt.strftime('%Y/%m/%d %H:%M:%S'))

        # Change mode, air flow direction, and set point temperature depends on season
        is_summer = 5 <= dt.month and dt.month <= 10
        mode = vrc.VRFCommunicator.Mode.Cooling if is_summer else vrc.VRFCommunicator.Mode.Heating
        dir = vrc.VRFCommunicator.Direction.Horizontal if is_summer else vrc.VRFCommunicator.Direction.Vertical
        sp = 26 if is_summer else 22

        # When the HVAC changed to operating hours
        if(not(is_hvac_time(last_dt)) and is_hvac_time(dt)):
            for i in range(len(i_unit_num)):
                for j in range(i_unit_num[i]):
                    v_name = 'VRF' + str(i + 1) + '-' + str(j+1)

                    print('Turning on ' + v_name + '...',end='')
                    rslt = vrCom.turn_on(i+1,j+1)
                    print('success' if rslt[0] else 'failed: ' + rslt[1])

                    print('Turning on ' + v_name + ' (Ventilation)...',end='')
                    rslt = vsCom.start_ventilation(i+1,j+1)
                    print('success' if rslt[0] else 'failed: ' + rslt[1])

                    print('Changing mode of ' + v_name + ' to ' + str(mode) + '...',end='')
                    rslt = vrCom.change_mode(i+1,j+1,mode)
                    print('success' if rslt[0] else 'failed: ' + rslt[1])

                    print('Changing set point temperature of ' + v_name + ' to ' + str(sp) + 'C...',end='')
                    rslt = vrCom.change_setpoint_temperature(i+1,j+1,sp)
                    print('success' if rslt[0] else 'failed: ' + rslt[1])

                    print('Changing fanspeed of ' + v_name + ' to Middle...',end='')
                    rslt = vrCom.change_fan_speed(i+1,j+1,vrc.VRFCommunicator.FanSpeed.Middle)
                    print('success' if rslt[0] else 'failed: ' + rslt[1])

                    print('Changing direction of ' + v_name + ' to ' + str(dir) + '...',end='')
                    rslt = vrCom.change_direction(i+1,j+1,dir)
                    print('success' if rslt[0] else 'failed: ' + rslt[1])

        # When the HVAC changed to stop hours
        if(is_hvac_time(last_dt) and not(is_hvac_time(dt))):
            for i in range(len(i_unit_num)):
                for j in range(i_unit_num[i]):
                    v_name = 'VRF' + str(i + 1) + '-' + str(j+1)

                    print('Turning off ' + v_name + '...',end='')
                    rslt = vrCom.turn_off(i+1,j+1)
                    print('success' if rslt else 'failed')

                    print('Turning off ' + v_name + ' (Ventilation)...',end='')
                    rslt = vsCom.stop_ventilation(i+1,j+1)
                    print('success' if rslt else 'failed')

        last_dt = dt # Save last date and time
        time.sleep(0.5)

def is_hvac_time(dtime):
    start_time = datetime.time(7, 0)
    end_time = datetime.time(19, 0)   
    now = dtime.time()
    is_business_hour = start_time <= now <= end_time
    is_weekday = (dtime.weekday() != 5 and dtime.weekday() != 6)
    return is_weekday and is_business_hour

if __name__ == "__main__":
    main()
