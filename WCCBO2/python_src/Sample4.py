import time
import VentilationSystemCommunicator as vsc

vCom = vsc.VentilationSystemCommunicator(16)

while True:
    print('Reading CO2 level of south tenant... ',end='')
    val = vCom.get_south_tenant_CO2_level()
    print(str(val[1]) if val[0] else ' Failed')

    print('Reading CO2 level of north tenant... ',end='')
    val = vCom.get_north_tenant_CO2_level()
    print(str(val[1]) if val[0] else ' Failed')

    print('Turning on HEX1-1... ',end='')
    val = vCom.start_ventilation(1,1)
    print('success' if val[0] else ' Failed')

    print('Turning off HEX1-1... ',end='')
    val = vCom.stop_ventilation(1,1)
    print('success' if val[0] else ' Failed')

    print('Reading fan speed of HEX1-1... ',end='')
    val = vCom.get_fan_speed(1,1)
    print(str(val[1]) if val[0] else ' Failed')

    print('Changing fan speed of HEX1-1 to Middle...',end='')
    rslt = vCom.change_fan_speed(1,1,vsc.VentilationSystemCommunicator.FanSpeed.Middle)
    print('success' if rslt[0] else 'failed')

    time.sleep(1)


