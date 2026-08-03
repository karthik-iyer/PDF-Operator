import { Routes } from '@angular/router';

import { AppShellComponent } from './app-shell/app-shell';
import { UploadListComponent } from './upload-list/upload-list';
import { Viewer } from './viewer/viewer';

export const routes: Routes = [
  {
    path: '',
    component: AppShellComponent,
    children: [
      { path: '', component: UploadListComponent },
      { path: 'viewer/:id', component: Viewer },
      { path: '**', redirectTo: '' }
    ]
  }
];
