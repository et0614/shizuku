import asyncio
from VentilationSystemCommunicator import VentilationSystemCommunicator as vsc

async def main():
    vCom = vsc(16)

    while True:
        print('Reading CO2 level of south tenant... ',end='')
        val = await vCom.get_south_tenant_CO2_level()
        print(str(val[1]) if val[0] else ' failed')

        print('Reading CO2 level of north tenant... ',end='')
        val = await vCom.get_north_tenant_CO2_level()
        print(str(val[1]) if val[0] else ' failed')

        print('Turning on HEX1-1... ',end='')
        val = await vCom.start_ventilation(1,1)
        print('success' if val[0] else ' failed')

        print('Turning off HEX1-1... ',end='')
        val = await vCom.stop_ventilation(1,1)
        print('success' if val[0] else ' failed')

        print('Reading fan speed of HEX1-1... ',end='')
        val = await vCom.get_fan_speed(1,1)
        print(str(val[1]) if val[0] else ' failed')

        print('Changing fan speed of HEX1-1 to Middle...',end='')
        rslt = await vCom.change_fan_speed(1,1,vsc.FanSpeed.Middle)
        print('success' if rslt[0] else 'failed')

        print('')
        await asyncio.sleep(1)

asyncio.run(main())
