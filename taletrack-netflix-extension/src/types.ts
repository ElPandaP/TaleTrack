export interface NetflixMedia {
  title: string;
  year?: number;
  type: 'movie' | 'series';
  genres: string[];
  duration?: string;
  description?: string;
  imageUrl?: string;
  netflixUrl: string;
  extractedAt: string;

  // Extra fields for series
  season?: number;
  episode?: number;
  episodeTitle?: string;
}

export interface ExtractDataMessage {
  action: 'extractData';
}

export interface ExtractDataResponse {
  success: boolean;
  data?: NetflixMedia;
  error?: string;
}