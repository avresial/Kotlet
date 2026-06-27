import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

// Kotlet brand uses warm terracotta/orange tones. Override the Aura preset's
// default emerald primary with an orange palette so buttons, links and other
// PrimeNG accents match the rest of the app.
export const KotletPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '{orange.50}',
      100: '{orange.100}',
      200: '{orange.200}',
      300: '{orange.300}',
      400: '{orange.400}',
      500: '{orange.500}',
      600: '{orange.600}',
      700: '{orange.700}',
      800: '{orange.800}',
      900: '{orange.900}',
      950: '{orange.950}',
    },
    colorScheme: {
      light: {
        primary: {
          color: '{orange.500}',
          contrastColor: '#ffffff',
          hoverColor: '{orange.600}',
          activeColor: '{orange.700}',
        },
      },
      dark: {
        primary: {
          color: '{orange.400}',
          contrastColor: '{orange.950}',
          hoverColor: '{orange.300}',
          activeColor: '{orange.200}',
        },
      },
    },
  },
});
