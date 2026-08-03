import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LucideFileText } from '@lucide/angular';

/**
 * Persistent app chrome: dark icon rail on the far left + routed content.
 * The viewer's page-thumbnail panel is separate and only appears inside the viewer route.
 */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, LucideFileText],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss'
})
export class AppShellComponent {}
