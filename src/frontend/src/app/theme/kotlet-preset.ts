import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

// Keep PrimeNG controls aligned with the same terracotta palette used by the
// application shell and marketing pages.
export const KotletPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#fff7f4',
      100: '#fce8e2',
      200: '#f8d0c5',
      300: '#eeaa97',
      400: '#df7e67',
      500: '#b9533f',
      600: '#a94735',
      700: '#8e392b',
      800: '#763126',
      900: '#622d25',
      950: '#351510',
    },
    colorScheme: {
      light: {
        primary: {
          color: '{primary.500}',
          contrastColor: '#ffffff',
          hoverColor: '{primary.600}',
          activeColor: '{primary.700}',
        },
      },
      dark: {
        primary: {
          color: '{primary.400}',
          contrastColor: '{primary.950}',
          hoverColor: '{primary.300}',
          activeColor: '{primary.200}',
        },
      },
    },
  },
});
