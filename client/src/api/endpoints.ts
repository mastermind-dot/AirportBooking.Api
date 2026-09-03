export { http } from './httpClient';
import { http } from './httpClient';
import type {
  Airport, AuthResponse, Booking, BookingSummary, CharterKind, CharterRequestResult,
  AircraftPreference, FlightSummary, PagedResult, PassengerInput, User,
} from '../types';

// ---------- auth ----------

export const authApi = {
  register: (body: {
    firstName: string; lastName: string; email: string; password: string; preferredLanguage: string;
  }) => http.post<AuthResponse>('/auth/register', body).then((r) => r.data),

  login: (body: { email: string; password: string }) =>
    http.post<AuthResponse>('/auth/login', body).then((r) => r.data),

  logout: () => http.post('/auth/logout'),

  me: () => http.get<User>('/auth/me').then((r) => r.data),
};

// ---------- scheduled flights ----------

export interface FlightSearchParams {
  origin: string;
  destination: string;
  departureDate: string;
  passengers: number;
  maxPrice?: number;
  departAfter?: string;
  sortBy?: 'DepartureTime' | 'Price' | 'Duration';
  page?: number;
  pageSize?: number;
}

export const flightsApi = {
  airports: () => http.get<Airport[]>('/airports').then((r) => r.data),

  search: (params: FlightSearchParams) =>
    http.get<PagedResult<FlightSummary>>('/flights/search', { params }).then((r) => r.data),

  byId: (id: string) => http.get(`/flights/${id}`).then((r) => r.data),
};

// ---------- bookings ----------

export const bookingsApi = {
  create: (body: {
    flightId: string;
    contactEmail: string;
    contactPhone?: string | null;
    passengers: PassengerInput[];
  }) => http.post<Booking>('/bookings', body).then((r) => r.data),

  mine: (page = 1, pageSize = 20) =>
    http.get<PagedResult<BookingSummary>>('/bookings', { params: { page, pageSize } })
      .then((r) => r.data),

  byId: (id: string) => http.get<Booking>(`/bookings/${id}`).then((r) => r.data),

  cancel: (id: string) => http.post<Booking>(`/bookings/${id}/cancel`).then((r) => r.data),
};

// ---------- charter enquiries ----------

export const chartersApi = {
  submit: (body: {
    kind: CharterKind;
    contactName: string;
    contactEmail: string;
    contactPhone?: string | null;
    company?: string | null;
    origin: string;
    destination: string;
    departureDate: string;
    returnDate?: string | null;
    preferredAircraft: AircraftPreference;
    passengerCount?: number | null;
    cargoWeightKg?: number | null;
    cargoDescription?: string | null;
    message?: string | null;
  }) => http.post<CharterRequestResult>('/charters', body).then((r) => r.data),

  mine: () => http.get<CharterRequestResult[]>('/charters/mine').then((r) => r.data),
};
