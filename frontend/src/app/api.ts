import { Injectable, inject } from "@angular/core";
import {
  HttpClient,
  HttpInterceptorFn,
  HttpErrorResponse,
} from "@angular/common/http";
import { CanActivateFn, Router } from "@angular/router";
import { firstValueFrom, catchError, throwError } from "rxjs";
import { API_URL } from "./config";
export interface User {
  id: string;
  name: string;
  nickname: string;
  role: string;
  elo: number;
}
export interface Player {
  id: string;
  nickname: string;
  elo: number;
}
export interface Tournament {
  id: string;
  name: string;
  status: string;
  capacity: number;
  organizerId: string;
  players: Player[];
  battleId: string | null;
}
export interface Battle {
  id: string;
  tournamentId: string;
  tournament: string;
  round: number;
  playerOne: Player;
  playerTwo: Player;
  status: string;
  startingPlayerId: string;
}
@Injectable({ providedIn: "root" })
export class Api {
  private http = inject(HttpClient);
  private router = inject(Router);
  token = sessionStorage.getItem("farmeort.token");
  user: User | null = null;
  get<T>(path: string) {
    return firstValueFrom(this.http.get<T>(API_URL + path));
  }
  post<T>(path: string, body: unknown = {}) {
    return firstValueFrom(this.http.post<T>(API_URL + path, body));
  }
  async restore() {
    if (this.token)
      try {
        this.user = await this.get<User>("/api/auth/me");
      } catch {
        this.clear();
      }
  }
  async login(body: unknown) {
    const s = await this.post<{ token: string; user: User }>(
      "/api/auth/login",
      body,
    );
    this.token = s.token;
    this.user = s.user;
    sessionStorage.setItem("farmeort.token", s.token);
  }
  clear() {
    this.token = null;
    this.user = null;
    sessionStorage.removeItem("farmeort.token");
  }
  async logout() {
    await this.post("/api/auth/logout");
    this.clear();
    await this.router.navigateByUrl("/login");
  }
}
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const api = inject(Api);
  const router = inject(Router);
  if (!req.url.startsWith(API_URL + "/")) return next(req);
  return next(
    api.token
      ? req.clone({ setHeaders: { Authorization: "Bearer " + api.token } })
      : req,
  ).pipe(
    catchError((e: HttpErrorResponse) => {
      if (e.status === 401 && !req.url.endsWith("/login")) {
        api.clear();
        void router.navigateByUrl("/login");
      }
      return throwError(() => e);
    }),
  );
};
export const authGuard: CanActivateFn = () =>
  inject(Api).user ? true : inject(Router).createUrlTree(["/login"]);
export const organizerGuard: CanActivateFn = () =>
  inject(Api).user?.role === "Organizador"
    ? true
    : inject(Router).createUrlTree(["/torneo"]);
export function errorMessage(e: unknown): string {
  const err = e as HttpErrorResponse;
  return err.status === 0
    ? "No se pudo conectar con la API. Revisá la conexión e intentá nuevamente."
    : err.error?.message ||
        "No se pudo completar la operación. Intentá nuevamente.";
}
