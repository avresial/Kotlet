export interface HouseMember {
  id: string;
  email: string;
  displayName: string | null;
  lastLoginAtUtc: string | null;
  isCurrentUser: boolean;
}

export interface House {
  id: string;
  name: string;
  members: HouseMember[];
}
