export interface ElitechRealtimeItem {
  deviceGuid: string;
  deviceName?: string;
  tmp1?: string;
  hum1?: string;
  power?: string;
  signal?: string;
  address?: string;
  lastSessionTime?: number;
  alarmState?: boolean;
  warnState?: boolean;
}

export interface ElitechRealtimeResponse {
  code: number;
  message?: string;
  data?: ElitechRealtimeItem[];
}



// Gọi API MVC (/api/me) để biết user đang đăng nhập
export type MeResp = { name: string | null; role: string | null }

export async function getMe(): Promise<MeResp | null> {
  const r = await fetch('/api/me', { credentials: 'include' })
  if (!r.ok) return null
  return r.json()
}

export async function fetchElitechRealtime(
  deviceGuids: string
): Promise<ElitechRealtimeResponse> {
  const res = await fetch(
    `/api/elitech/realtime?deviceGuids=${encodeURIComponent(deviceGuids)}`,
    { credentials: 'include' }
  );
  if (!res.ok) throw new Error(`Realtime API failed: ${res.status}`);
  return res.json() as Promise<ElitechRealtimeResponse>;
}
export async function getElitechHistory(deviceGuid: string, lastHours = 24) {
  const url = `/api/elitech/history?deviceGuid=${encodeURIComponent(deviceGuid)}&lastHours=${lastHours}`;
  const r = await fetch(url, { credentials: 'include' });
  if (!r.ok) throw new Error(`Realtime API failed: ${r.status}`);
  return r.json(); // { code, data: HistoryItem[] }
}
