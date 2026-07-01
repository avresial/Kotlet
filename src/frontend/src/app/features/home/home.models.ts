export interface HouseMember {
  id: string;
  email: string;
  displayName: string | null;
  lastLoginAtUtc: string | null;
  isCurrentUser: boolean;
}

export interface PendingInvitation {
  id: string;
  email: string;
  displayName: string | null;
  invitedAtUtc: string;
}

export interface HomeSummary {
  id: string;
  name: string;
  memberCount: number;
  isDefault: boolean;
  isActive: boolean;
}

export interface HomeDetail {
  id: string;
  name: string;
  members: HouseMember[];
  pendingInvitations: PendingInvitation[];
}

export interface DashboardStats {
  recipeCount: number;
  pantryItemCount: number;
}

export interface IncomingInvitation {
  id: string;
  houseId: string;
  houseName: string;
  invitedByName: string;
  invitedAtUtc: string;
}

export interface TokenResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
}

export interface HouseWithToken {
  house: HomeSummary;
  token: TokenResponse | null;
}
