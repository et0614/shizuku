import datetime, asyncio
from VentilationSystemCommunicator import VentilationSystemCommunicator as vsc

async def main():
    vsCom = vsc(26)

    # Enable current_date_time method
    print('Subscribe COV...')
    await vsCom.subscribe_date_time_cov()

    # Number of indoor units in each VRF system
    i_unit_num = [5,4,5,4]

    while True:
        # Output current date and time
        dt = vsCom.current_date_time()
        print(dt.strftime('%Y/%m/%d %H:%M:%S'))

        if(is_hvac_time(dt)):
            # Get CO2 level
            val = await vsCom.get_south_tenant_CO2_level()
            south_co2 = val[1] if val[0] else 1000
            val = await vsCom.get_north_tenant_CO2_level()
            north_co2 = val[1] if val[0] else 1000

            # Switch fan speed
            south_fs = get_fan_speed(south_co2)
            north_fs = get_fan_speed(north_co2)

            # Output status
            print('South tenant: ' + str(south_fs) + ' (' + str(south_co2) + ')')
            print('North tenant: ' + str(north_fs) + ' (' + str(north_co2) + ')')

            # Change fan speed
            for i in range(len(i_unit_num)):
                fs = south_fs if i == 0 or i==1 else north_fs
                for j in range(i_unit_num[i]):
                    val = await vsCom.change_fan_speed(i+1,j+1,fs)
        await asyncio.sleep(1.0)

def get_fan_speed(co2_level):
    if co2_level < 600:
        return vsc.FanSpeed.Low
    elif co2_level < 800:
        return vsc.FanSpeed.Middle
    else:
        return vsc.FanSpeed.High

def is_hvac_time(dtime):
    start_time = datetime.time(7, 0)
    end_time = datetime.time(19, 0)   
    now = dtime.time()
    is_business_hour = start_time <= now <= end_time
    is_weekday = (dtime.weekday() != 5 and dtime.weekday() != 6)
    return is_weekday and is_business_hour

if __name__ == "__main__":
    asyncio.run(main())
