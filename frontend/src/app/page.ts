import { Component, OnInit, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
  AbstractControl,
} from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { Api, Battle, Tournament, errorMessage } from "./api";
function age(c: AbstractControl) {
  const value = c.value as string;
  if (!value) return null;
  const today = new Date();
  const year = today.getUTCFullYear() - 13;
  const month = today.getUTCMonth();
  const day = Math.min(
    today.getUTCDate(),
    new Date(Date.UTC(year, month + 1, 0)).getUTCDate(),
  );
  return new Date(value + "T00:00:00Z") <= new Date(Date.UTC(year, month, day))
    ? null
    : { age: true };
}
function strongPassword(c: AbstractControl) {
  const p = c.value as string;
  return /\p{Lu}/u.test(p) &&
    /\p{Ll}/u.test(p) &&
    /\p{Nd}/u.test(p) &&
    /[^\p{L}\p{N}\s]/u.test(p)
    ? null
    : { complexity: true };
}
@Component({
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  selector: "app-page",
  templateUrl: "./page.html",
})
export class Page implements OnInit {
  api = inject(Api);
  private fb = inject(FormBuilder);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  page = "";
  busy = false;
  error = "";
  notice = "";
  tournament: Tournament | null = null;
  battle: Battle | null = null;
  form = this.fb.nonNullable.group({
    name: [
      "",
      [
        Validators.required,
        Validators.maxLength(100),
        Validators.pattern(/\S/),
      ],
    ],
    surname: [
      "",
      [
        Validators.required,
        Validators.minLength(3),
        Validators.maxLength(25),
        Validators.pattern(/^\S(?:.*\S)?$/),
      ],
    ],
    email: [
      "",
      [Validators.required, Validators.email, Validators.maxLength(254)],
    ],
    nickname: [
      "",
      [Validators.required, Validators.pattern(/^[a-zA-Z0-9]{3,20}$/)],
    ],
    password: [
      "",
      [
        Validators.required,
        Validators.minLength(15),
        Validators.maxLength(25),
        strongPassword,
      ],
    ],
    birthDate: ["", [Validators.required, age]],
  });
  loginForm = this.fb.nonNullable.group({
    email: ["", [Validators.required, Validators.email]],
    password: ["", Validators.required],
  });
  async ngOnInit() {
    this.route.data.subscribe((data) => {
      this.page = data["page"];
      this.error = "";
      this.notice = "";
      this.tournament = null;
      this.battle = null;
      if (this.page === "tournament" || this.page === "organizer")
        void this.refresh();
      if (this.page === "battle") void this.loadBattle();
    });
  }
  invalid(field: keyof typeof this.form.controls) {
    const c = this.form.controls[field];
    return c.touched && c.invalid;
  }
  async run(action: () => Promise<void>) {
    if (this.busy) return;
    this.busy = true;
    this.error = "";
    try {
      await action();
    } catch (e) {
      this.error = errorMessage(e);
    } finally {
      this.busy = false;
    }
  }
  async register() {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    await this.run(async () => {
      await this.api.post("/api/auth/register", this.form.getRawValue());
      await this.router.navigateByUrl("/login?registered=1");
    });
  }
  async login() {
    this.loginForm.markAllAsTouched();
    if (this.loginForm.invalid) return;
    await this.run(async () => {
      await this.api.login(this.loginForm.getRawValue());
      await this.router.navigateByUrl(
        this.api.user?.role === "Organizador" ? "/organizador" : "/torneo",
      );
    });
  }
  registered() {
    return this.route.snapshot.queryParamMap.has("registered");
  }
  refresh() {
    return this.run(async () => {
      this.tournament = await this.api.get<Tournament>("/api/tournaments/demo");
    });
  }
  join() {
    return this.run(async () => {
      this.tournament = await this.api.post<Tournament>(
        "/api/tournaments/" + this.tournament!.id + "/registrations",
      );
      this.notice =
        "¡Inscripción aceptada! Ya ocupás el segundo lugar de la copa.";
    });
  }
  generate() {
    return this.run(async () => {
      const b = await this.api.post<Battle>(
        "/api/tournaments/" + this.tournament!.id + "/round",
      );
      await this.router.navigate(["/batalla", b.id]);
    });
  }
  loadBattle() {
    return this.run(async () => {
      this.battle = await this.api.get<Battle>(
        "/api/battles/" + this.route.snapshot.paramMap.get("id"),
      );
    });
  }
  get joined() {
    return this.tournament?.players.some((p) => p.id === this.api.user?.id);
  }
  get starter() {
    return this.battle?.startingPlayerId === this.battle?.playerOne.id
      ? this.battle?.playerOne.nickname
      : this.battle?.playerTwo.nickname;
  }
}
