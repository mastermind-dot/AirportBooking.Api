/**
 * The fleet, as published by the company.
 *
 * Held as front-end data rather than in the database on purpose: these are two
 * fixed aircraft described in marketing copy, not bookable inventory. The
 * Flights table models what can be sold; this models what the company owns.
 * If the fleet ever drives availability, it moves server-side.
 */

export type AircraftId = 'sd360' | 'g159';

export interface Aircraft {
  id: AircraftId;
  /** Manufacturer designation as the company writes it. */
  name: string;
  image: string;
  /** Base airport, from the company history. */
  base: 'GOMA' | 'KINSHASA';
  /** How many of this type are operated. */
  count: number;
  passengers: number;
  cargoKg: number;
  /** Cruise speed in km/h. */
  cruiseKmh: number;
  /** Endurance in hours of flight. */
  enduranceHours: number;
  /** Only quoted for the G159 on the live site. */
  rangeKm?: number;
  /** Only quoted for the SD360. */
  cargoVolumeM3?: number;
  /** Translation keys for the type's selling points. */
  highlightKeys: string[];
}

export const FLEET: Aircraft[] = [
  {
    id: 'sd360',
    name: 'Short SD360',
    image: '/images/sd360.png',
    base: 'GOMA',
    count: 2,
    passengers: 30,
    cargoKg: 3500,
    cruiseKmh: 300,
    enduranceHours: 4,
    cargoVolumeM3: 30,
    highlightKeys: ['shortField', 'cargoDoor', 'remoteAccess'],
  },
  {
    id: 'g159',
    name: 'Gulfstream G159',
    image: '/images/g159.png',
    base: 'KINSHASA',
    count: 1,
    passengers: 24,
    cargoKg: 3000,
    cruiseKmh: 400,
    enduranceHours: 6,
    rangeKm: 1800,
    highlightKeys: ['speed', 'altitude', 'comfort'],
  },
];

export const findAircraft = (id: string): Aircraft | undefined =>
  FLEET.find((aircraft) => aircraft.id === id);
