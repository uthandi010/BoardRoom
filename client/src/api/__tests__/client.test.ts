import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { apiRequest, ApiError, getToken, setToken } from "../client";

describe("apiRequest", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.stubGlobal("fetch", vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("does not send an Authorization header when there is no token", async () => {
    setToken(null);
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValue(
      new Response(JSON.stringify({ ok: true }), { status: 200 })
    );

    await apiRequest("/api/anything");

    const [, init] = (fetch as ReturnType<typeof vi.fn>).mock.calls[0];
    expect((init.headers as Record<string, string>).Authorization).toBeUndefined();
  });

  it("attaches a Bearer Authorization header when a token is stored", async () => {
    setToken("test-token-123");
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValue(
      new Response(JSON.stringify({ ok: true }), { status: 200 })
    );

    await apiRequest("/api/anything");

    const [, init] = (fetch as ReturnType<typeof vi.fn>).mock.calls[0];
    expect((init.headers as Record<string, string>).Authorization).toBe("Bearer test-token-123");
  });

  it("returns the parsed JSON body on success", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValue(
      new Response(JSON.stringify({ id: 1, name: "Acme" }), { status: 200 })
    );

    const result = await apiRequest<{ id: number; name: string }>("/api/workspaces/1");

    expect(result).toEqual({ id: 1, name: "Acme" });
  });

  it("returns undefined for a 204 No Content response", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValue(new Response(null, { status: 204 }));

    const result = await apiRequest("/api/cards/1", { method: "DELETE" });

    expect(result).toBeUndefined();
  });

  it("throws an ApiError with the server's message on a failed request", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValue(
      new Response(JSON.stringify({ message: "Invalid email or password." }), { status: 401 })
    );

    await expect(apiRequest("/api/auth/login", { method: "POST" })).rejects.toMatchObject({
      message: "Invalid email or password.",
      status: 401,
    });
  });

  it("falls back to a generic message when the server sends no message", async () => {
    (fetch as ReturnType<typeof vi.fn>).mockResolvedValue(new Response("", { status: 500 }));

    await expect(apiRequest("/api/anything")).rejects.toBeInstanceOf(ApiError);
  });

  it("round-trips the token through localStorage", () => {
    setToken("abc123");
    expect(getToken()).toBe("abc123");
    setToken(null);
    expect(getToken()).toBeNull();
  });
});
