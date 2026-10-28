import { APP_INITIALIZER, Component, inject } from "@angular/core";
import { bootstrapApplication } from "@angular/platform-browser";
import { provideHttpClient, withInterceptors } from "@angular/common/http";
import {
  Router,
  RouterLink,
  RouterOutlet,
  provideRouter,
} from "@angular/router";
import { CommonModule } from "@angular/common";
import {
  Api,
  authGuard,
  organizerGuard,
  authInterceptor,
  errorMessage,
} from "./app/api";
import { Page } from "./app/page";
@Component({
  selector: "app-root",
  standalone: true,
  imports: [RouterOutlet, RouterLink, CommonModule],
  template: `<header>
      <a class="brand" routerLink="/torneo"
        ><span class="brand-icon">✦</span>FarmeORT<span class="brand-sub"
          >BATALLAS DE AURA</span
        ></a
      >
      <nav *ngIf="api.user as u">
        <a routerLink="/torneo">La copa</a
        ><a *ngIf="u.role === 'Organizador'" routerLink="/organizador"
          >Organizar</a
        ><span class="user-name">{{ u.nickname }}</span
        ><button class="logout" (click)="logout()" [disabled]="busy">
          Cerrar sesión
        </button>
      </nav>
      <nav *ngIf="!api.user">
        <a routerLink="/login">Ingresar</a
        ><a class="nav-register" routerLink="/registro">Crear jugador ↗</a>
      </nav>
    </header>
    <main>
      <p *ngIf="error" class="alert error" role="alert">{{ error }}</p>
      <router-outlet />
    </main>
    <footer>
      <span>FARMEORT <b>✦</b> DISEÑO DE APLICACIONES 2</span
      ><span>ORT Uruguay · Demo educativa de despliegue</span>
      <p>
        Alcance: registro, sesión, inscripción y generación de una batalla
        pendiente. Sin skills, arbitraje ni cambios de ELO.
      </p>
    </footer>`,
})
class App {
  api = inject(Api);
  busy = false;
  error = "";
  async logout() {
    this.busy = true;
    this.error = "";
    try {
      await this.api.logout();
    } catch (e) {
      this.error = errorMessage(e);
    } finally {
      this.busy = false;
    }
  }
}
bootstrapApplication(App, {
  providers: [
    provideHttpClient(withInterceptors([authInterceptor])),
    {
      provide: APP_INITIALIZER,
      useFactory: (api: Api) => () => api.restore(),
      deps: [Api],
      multi: true,
    },
    provideRouter([
      { path: "registro", component: Page, data: { page: "register" } },
      { path: "login", component: Page, data: { page: "login" } },
      {
        path: "torneo",
        component: Page,
        data: { page: "tournament" },
        canActivate: [authGuard],
      },
      {
        path: "organizador",
        component: Page,
        data: { page: "organizer" },
        canActivate: [authGuard, organizerGuard],
      },
      {
        path: "batalla/:id",
        component: Page,
        data: { page: "battle" },
        canActivate: [authGuard],
      },
      { path: "**", redirectTo: "torneo" },
    ]),
  ],
}).catch(console.error);
