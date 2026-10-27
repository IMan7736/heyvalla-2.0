import type { components } from "@dhathuru/contracts";

type Schemas = components["schemas"];

export type Booking = Schemas["BookingDetail"];
export type BookingLeg = Schemas["BookingLegDetail"];
export type BookingStatus = Schemas["BookingStatus"];
export type BookingLegStatus = Schemas["BookingLegStatus"];
export type Payment = Schemas["PaymentDetail"];
export type PriceLine = Schemas["PriceLine"];
export type BookingDisruption = Schemas["BookingDisruption"];
export type Ticket = Schemas["TicketDetail"];
export type CancellationQuote = Schemas["CancellationQuote"];
export type Money = Schemas["Money"];
export type Leg = Schemas["Leg"];
export type Passenger = Schemas["Passenger"];
export type PassengerType = Schemas["PassengerType"];
export type Traveller = Schemas["SavedTraveller"];
export type TripOption = Schemas["TripOption"];

export type PassengerInput = {
  travellerId?: string;
  fullName: string;
  passengerType: PassengerType;
  phone?: string;
};

export type BookingPage = { items: Booking[]; page: { nextCursor?: string | null; limit: number } };
