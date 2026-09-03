/**
 * Shapes returned by the API.
 *
 * Enums arrive as names, not ordinals — the API serialises them with
 * JsonStringEnumConverter — so these are string unions rather than numbers.
 */

export type BookingStatus = 'Pending' | 'Confirmed' | 'Cancelled' | 'Failed';
export type PaymentStatus = 'Pending' | 'Succeeded' | 'Failed' | 'Refunded';
export type CharterKind = 'Passenger' | 'Cargo';
export type CharterStatus = 'New' | 'Contacted' | 'Quoted' | 'Closed';
export type AircraftPreference = 'Any' | 'ShortSd360' | 'GulfstreamG159';

export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  preferredLanguage: string;
}

export interface AuthResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: User;
}

export interface Airport {
  iataCode: string;
  name: string;
  city: string;
  countryCode: string;
}

export interface FlightSummary {
  id: string;
  flightNumber: string;
  airlineName: string;
  aircraftType: string;
  airlineIataCode: string;
  origin: Airport;
  destination: Airport;
  departureTimeUtc: string;
  /** Wall-clock time at the origin — what a traveller reads on the ticket. */
  departureTimeLocal: string;
  arrivalTimeUtc: string;
  arrivalTimeLocal: string;
  durationMinutes: number;
  stops: number;
  isDirect: boolean;
  pricePerPassenger: number;
  totalPrice: number;
  currency: string;
  availableSeats: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface PassengerInput {
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  nationality: string;
  passportNumber: string;
  passportExpiry?: string | null;
}

export interface BookingFlight {
  id: string;
  flightNumber: string;
  airlineName: string;
  originIata: string;
  originCity: string;
  destinationIata: string;
  destinationCity: string;
  departureTimeUtc: string;
  departureTimeLocal: string;
  arrivalTimeUtc: string;
  arrivalTimeLocal: string;
  durationMinutes: number;
  stops: number;
}

export interface Booking {
  id: string;
  reference: string;
  status: BookingStatus;
  totalAmount: number;
  currency: string;
  contactEmail: string;
  contactPhone?: string | null;
  createdAtUtc: string;
  confirmedAtUtc?: string | null;
  cancelledAtUtc?: string | null;
  flight: BookingFlight;
  passengers: Array<PassengerInput & { id: string; seatNumber?: string | null }>;
  paymentStatus?: PaymentStatus | null;
}

export interface BookingSummary {
  id: string;
  reference: string;
  status: BookingStatus;
  totalAmount: number;
  currency: string;
  passengerCount: number;
  createdAtUtc: string;
  flight: BookingFlight;
}

export interface CharterRequestResult {
  id: string;
  reference: string;
  kind: CharterKind;
  status: CharterStatus;
  origin: string;
  destination: string;
  departureDate: string;
  returnDate?: string | null;
  createdAtUtc: string;
}

/** RFC 7807 problem+json, plus the machine-readable code the API adds. */
export interface ApiProblem {
  title?: string;
  detail?: string;
  status?: number;
  code?: string;
  errors?: Record<string, string[]>;
}
