// Ham goi API dung chung: them header, doc loi tra ve thanh cau de hieu
export class ApiError extends Error {
  constructor(message: string, public status: number) {
    super(message);
  }
}

// Qua 15 giay khong co tra loi thi bao loi, thay vi de app quay mai (hay gap tren dien thoai khi sai mang)
const TIMEOUT_MS = 15_000;
// Rieng AI Assistant: mo hinh AI co the mat 20-40 giay de tra loi nen cho lau hon.
// Matched by path (not by AI_URL) so it also works when every URL is the API Gateway.
const AI_TIMEOUT_MS = 90_000;

export async function requestJson<T>(url: string, options: RequestInit = {}): Promise<T> {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), url.includes("/api/ai/") ? AI_TIMEOUT_MS : TIMEOUT_MS);
  let res: Response;
  try {
    res = await fetch(url, {
      ...options,
      signal: controller.signal,
      headers: { "Content-Type": "application/json", ...options.headers },
    });
  } catch {
    const host = url.split("/").slice(0, 3).join("/");
    throw new ApiError(`Can't reach the server at ${host}. Check that the service is running.`, 0);
  } finally {
    clearTimeout(timer);
  }

  if (!res.ok) {
    let message = `Request failed (${res.status}).`;
    try {
      const body = await res.json();
      if (body?.message) message = body.message;
      else if (body?.errors) message = Object.values(body.errors as Record<string, string[]>).flat().join(" ");
    } catch {
      // Body khong phai JSON
    }
    throw new ApiError(message, res.status);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

// Ham goi API can dang nhap, do AuthContext cung cap (tu gan token + tu gia han token)
export type AuthFetch = <T>(url: string, options?: RequestInit) => Promise<T>;
