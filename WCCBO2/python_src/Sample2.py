import time
import EnvironmentCommunicator

eCom = EnvironmentCommunicator.EnvironmentCommunicator(14)

while True:
    print('Reading outdoor air temperature... ',end='')
    val = eCom.get_drybulb_temperature()
    print('{:.1f}'.format(val[1]) + ' C' if val[0] else ' Failed')

    print('Reading outdoor relative humidity... ',end='')
    val = eCom.get_relative_humidity()
    print('{:.1f}'.format(val[1]) + ' %' if val[0] else ' Failed')

    print('Reading global horizontal radiation... ',end='')
    val = eCom.get_global_horizontal_radiation()
    print('{:.1f}'.format(val[1]) + ' W/m2' if val[0] else ' Failed')

    print('Reading drybulb temperature of zone at VRF2-4... ',end='')
    val = eCom.get_zone_drybulb_temperature(2,4)
    print('{:.1f}'.format(val[1]) + ' C' if val[0] else ' Failed')

    print('Reading relative humidity of zone at VRF2-4... ',end='')
    val = eCom.get_zone_relative_humidity(2,4)
    print('{:.1f}'.format(val[1]) + ' %' if val[0] else ' Failed')

    time.sleep(1)

