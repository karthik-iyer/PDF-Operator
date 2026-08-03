import { bootstrapApplication } from '@angular/platform-browser';
import { pdfDefaultOptions } from 'ngx-extended-pdf-viewer';

import { appConfig } from './app/app.config';
import { App } from './app/app';

// The installed ngx-extended-pdf-viewer version ships its pdf.js assets under the
// "bleeding-edge" release channel (see angular.json's asset mapping), so the library's
// default lookup path ("./assets/") must be overridden to match, or it 404s on the viewer
// bundle/worker scripts at runtime.
pdfDefaultOptions.assetsFolder = 'bleeding-edge';

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
