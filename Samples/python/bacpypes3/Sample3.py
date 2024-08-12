import asyncio
from OccupantCommunicator import OccupantCommunicator as occ

async def main():
    oCom = occ(15)

    while True:
        print('Reading occupant number in north tenant... ',end='')
        val = await oCom.get_occupant_number(occ.Tenant.North)
        print(str(val[1]) if val[0] else ' Failed')

        print('Reading occupant number in south tenant zone-1... ',end='')
        val = await oCom.get_zone_occupant_number(occ.Tenant.South,1)
        print(str(val[1]) if val[0] else ' Failed')

        print('Reading averaged thermal sensation (south tenant zone-1)... ',end='')
        val = await oCom.get_averaged_thermal_sensation(occ.Tenant.South,1)
        print('{:.2f}'.format(val[1]) if val[0] else ' Failed')

        print('Reading averaged clothing index (south tenant zone-1)... ',end='')
        val = await oCom.get_averaged_clothing_index(occ.Tenant.South,1)
        print('{:.2f}'.format(val[1]) if val[0] else ' Failed')

        print('Is occupant No.1 in south tenant stay in office? ... ',end='')
        val = await oCom.is_occupant_stay_in_office(occ.Tenant.South, 1)
        print(str(val[1]) if val[0] else ' Failed')

        print('Reading thermal sensation of occupant No.2 in south tenant... ',end='')
        val = await oCom.get_thermal_sensation(occ.Tenant.South, 2)
        print(str(val[1]) if val[0] else ' Failed')

        print('Reading clothing index of occupant No.3 in south tenant... ',end='')
        val = await oCom.get_clothing_index(occ.Tenant.South, 3)
        print('{:.2f}'.format(val[1]) + ' Clo' if val[0] else ' Failed')

        print('Reading thermally dissatisfied rate (south tenant zone-2)... ',end='')
        val = await oCom.get_thermally_dissatisfied_rate(occ.Tenant.South,2)
        print('{:.2f}'.format(val[1]) if val[0] else ' Failed')

        print('Reading dissatisfied rate caused by draft (north tenant zone-4)... ',end='')
        val = await oCom.get_dissatisfied_rate_caused_by_draft(occ.Tenant.North,4)
        print('{:.2f}'.format(val[1]) if val[0] else ' Failed')

        print('Reading thermally dissatisfied rate (south tenant zone-2)... ',end='')
        val = await oCom.get_dissatisfied_rate_caused_by_vertical_temperature_distribution(occ.Tenant.North,5)
        print('{:.2f}'.format(val[1]) if val[0] else ' Failed')

        print('')
        await asyncio.sleep(1)

asyncio.run(main())

