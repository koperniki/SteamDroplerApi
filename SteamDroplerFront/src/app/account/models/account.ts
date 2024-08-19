export interface Account {
  name: string;
  isPlaying: boolean;
  config: AccountConfig;
  runConfig: AccountRunConfig;
}

export interface AccountConfig {
  idleEnable: boolean;
  steamId?: number;
  authType?: string;
}

export interface AccountRunConfig {
  lastLoginErrorTime?: Date;
  loginErrorReason?: string;
  lastErrorTime?: Date;
  errorReason?: string;
  updateTime?: Date;
  ownedApps?: number[];
  notOwnedApps?: number[];
  appsToAdd?: number[];
  packagesToAdd?: number[];
}
