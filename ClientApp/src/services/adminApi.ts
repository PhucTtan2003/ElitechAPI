// adminApi.ts

export type ApiResp<T> = { code: number; message?: string; data: T };

export type UserItem = { userId: string; username: string };

export type AssignmentItem = {
  deviceGuid: string;
  deviceName?: string;
  assignedAtUtc?: string;
};

export type DeviceItem = {
  deviceGuid: string;
  deviceName?: string;
};

async function requestJson<T>(input: RequestInfo, init?: RequestInit): Promise<T> {
  const r = await fetch(input, {
    credentials: "include",
    ...init,
    headers: {
      ...(init?.headers ?? {}),
    },
  });

  // đọc body trước để debug dễ hơn
  const text = await r.text();

  if (!r.ok) {
    throw new Error(`${(init?.method ?? "GET")} ${input} failed: ${r.status} ${text}`);
  }

  // nếu server trả rỗng
  if (!text) return {} as T;

  try {
    return JSON.parse(text) as T;
  } catch {
    // fallback nếu server trả HTML
    throw new Error(`Invalid JSON from ${(init?.method ?? "GET")} ${input}: ${text.slice(0, 200)}`);
  }
}

// =======================
// Admin (MVC endpoints)
// =======================

export async function fetchUsers() {
  return requestJson<ApiResp<UserItem[]>>("/Admin/GetUsers");
}
export async function fetchAllDevices() {
  const r = await fetch("/api/elitech/all-devices", { credentials: "include" });
  if (!r.ok) throw new Error(`all-devices failed: ${r.status}`);
  return r.json() as Promise<{ code: number; data: { deviceGuid: string; deviceName?: string }[] }>;
}

export async function fetchAssignmentsByUser(userId: string) {
  const q = encodeURIComponent(userId);
  return requestJson<ApiResp<AssignmentItem[]>>(`/Admin/GetAssignments?userId=${q}`);
}

export async function assignDevice(userId: string, deviceGuid: string, deviceName?: string) {
  return requestJson<ApiResp<null>>("/Admin/AssignDevices", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userId, deviceGuid, deviceName }),
  });
}

export async function unassignDevice(userId: string, deviceGuid: string) {
  return requestJson<ApiResp<null>>("/Admin/UnassignDevice", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ userId, deviceGuid }),
  });
}


