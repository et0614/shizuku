import asyncio
from VRFSystemCommunicator import VRFSystemCommunicator as vrc

async def main():
    vCom = vrc(12)

    while True:
        print('Reading return air temperature of VRF1-2...',end='')
        rslt = await vCom.get_return_air_temperature(1,2)
        print(str(rslt[1]) + ' C' if rslt[0] else 'failed')

        print('Reading return air relative humidity of VRF1-2...',end='')
        rslt = await vCom.get_return_air_relative_humidity(1,2)
        print(str(rslt[1]) + ' %' if rslt[0] else 'failed')
        
        print('Turning on VRF1-2...',end='')
        rslt = await vCom.turn_on(1,2)
        print('success' if rslt[0] else 'failed')

        print('Turning off VRF1-2...',end='')
        rslt = await vCom.turn_off(1,2)
        print('success' if rslt[0] else 'failed')

        print('Changing mode of VRF1-2 to cooling...',end='')
        rslt = await vCom.change_mode(1,2,vrc.Mode.Cooling)
        print('success' if rslt[0] else 'failed')

        print('Changing set point temperature of VRF1-2 to 26C...',end='')
        rslt = await vCom.change_setpoint_temperature(1,2,26)
        print('success' if rslt[0] else 'failed')

        print('Changing fan speed of VRF1-2 to high...',end='')
        rslt = await vCom.change_fan_speed(1,2,vrc.FanSpeed.High)
        print('success' if rslt[0] else 'failed')

        print('Changing direction of VRF1-2 to 45degree...',end='')
        rslt = await vCom.change_direction(1,2,vrc.Direction.Degree_450)
        print('success' if rslt[0] else 'failed')

        print('Permitting local control of VRF1-2...',end='')
        rslt = await vCom.permit_local_control(1,2)
        print('success' if rslt[0] else 'failed')

        print('Prohibiting local control of VRF1-2...',end='')
        rslt = await vCom.prohibit_local_control(1,2)
        print('success' if rslt[0] else 'failed')

        print('')
        await asyncio.sleep(1)

asyncio.run(main())