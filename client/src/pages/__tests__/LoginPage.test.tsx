import { MemoryRouter } from "react-router-dom";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { LoginPage } from "../LoginPage";
import { ApiError } from "../../api/client";

const loginMock = vi.fn();
const navigateMock = vi.fn();

vi.mock("../../auth/AuthContext", () => ({
  useAuth: () => ({
    login: loginMock,
    register: vi.fn(),
    logout: vi.fn(),
    user: null,
    isAuthenticated: false,
  }),
}));

vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual<typeof import("react-router-dom")>("react-router-dom");
  return { ...actual, useNavigate: () => navigateMock };
});

describe("LoginPage", () => {
  beforeEach(() => {
    loginMock.mockReset();
    navigateMock.mockReset();
  });

  it("shows a validation error and does not call login when fields are empty", async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    );

    await user.click(screen.getByRole("button", { name: /log in/i }));

    expect(await screen.findByRole("alert")).toHaveTextContent(/required/i);
    expect(loginMock).not.toHaveBeenCalled();
  });

  it("logs in and navigates to /workspaces on valid submit", async () => {
    loginMock.mockResolvedValue(undefined);
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    );

    await user.type(screen.getByLabelText(/email/i), "person@example.com");
    await user.type(screen.getByLabelText(/password/i), "Password123!");
    await user.click(screen.getByRole("button", { name: /log in/i }));

    await waitFor(() =>
      expect(loginMock).toHaveBeenCalledWith("person@example.com", "Password123!")
    );
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith("/workspaces"));
  });

  it("shows the server's error message when login fails", async () => {
    loginMock.mockRejectedValue(new ApiError(401, "Invalid email or password."));
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <LoginPage />
      </MemoryRouter>
    );

    await user.type(screen.getByLabelText(/email/i), "person@example.com");
    await user.type(screen.getByLabelText(/password/i), "WrongPassword!");
    await user.click(screen.getByRole("button", { name: /log in/i }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Invalid email or password.");
    expect(navigateMock).not.toHaveBeenCalled();
  });
});
