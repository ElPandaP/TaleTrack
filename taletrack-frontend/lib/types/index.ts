// User types
export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  success: boolean;
  message: string;
  token: string;
}

export interface RegisterRequest {
  email: string;
  username: string;
  password: string;
}

export interface RegisterResponse {
  success: boolean;
  message: string;
}

// Media types
export type MediaType = 'Film' | 'Serie' | 'Book' | 'Comic';

export interface Media {
  id: number;
  title: string;
  type: MediaType;
  length?: number;
  description?: string;
  posterUrl?: string;
  firstTrackedAt: string;
  updatedAt: string;
}

export interface AddMediaRequest {
  title: string;
  type: MediaType;
  length?: number;
}

export interface AddMediaResponse {
  success: boolean;
  message: string;
}

// TrackingEvent types
export interface TrackingEvent {
  id: number;
  userId: number;
  mediaId: number;
  progress: number;
  eventDate: string;
  media?: Media;
}

export interface GetTrackingEventsRequest {
  type?: MediaType;
  limit?: number;
  orderBy?: string;
}

export interface GetTrackingEventsResponse {
  success: boolean;
  count: number;
  data: TrackingEvent[];
}

// Review types
export interface Review {
  id: number;
  userId: number;
  mediaId: number;
  rating: number;
  comment?: string;
  createdAt: string;
  updatedAt: string;
}

export interface AddReviewRequest {
  mediaId: number;
  rating: number;
  comment?: string;
}

export interface AddReviewResponse {
  success: boolean;
  message: string;
}
