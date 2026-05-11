<script>
  import { onMount } from 'svelte';

  let { formId = 'customer-intake-form' } = $props();

  let form;
  let values = $state({});
  let completion = $state(0);
  let codePreview = $state('CLIENT-0000');
  let waiverSigned = $state(false);
  let signatureRequired = $state(false);

  const requiredFields = ['FirstName', 'LastName', 'LicenseNumber', 'PhoneNumber', 'Email', 'City', 'Country', 'ElectronicSignature'];

  function readForm() {
    if (!form) {
      return;
    }

    values = Object.fromEntries(new FormData(form).entries());
    const waiverInput = form.querySelector('input[name="LiabilityWaiverSigned"][type="checkbox"]');
    waiverSigned = waiverInput?.checked ?? false;
    signatureRequired = waiverSigned;

    const filledRequiredCount = requiredFields.filter((field) => String(values[field] || '').trim().length > 0).length;
    completion = Math.round((filledRequiredCount / requiredFields.length) * 100);
    codePreview = buildCodePreview(values.FirstName, values.LastName);

    const signatureInput = form.querySelector('input[name="ElectronicSignature"]');
    if (signatureInput) {
      const dimmableContainer = form.querySelector('[data-signature-dimmable]');
      signatureInput.required = waiverSigned;
      signatureInput.disabled = !waiverSigned;
      dimmableContainer?.classList.toggle('opacity-50', !waiverSigned);

      if (!waiverSigned && signatureInput.value) {
        signatureInput.value = '';
      }
    }
  }

  function buildCodePreview(firstName, lastName) {
    const source = String(firstName || lastName || 'CLIENT')
      .replace(/[^a-z0-9]/gi, '')
      .slice(0, 6)
      .toUpperCase();

    return `${source || 'CLIENT'}-####`;
  }

  // ITU-T country code lengths by leading digit(s)
  function getCountryCodeLength(digits) {
    const d = digits;
    // Zone 1 - NANP (+1)
    if (d[0] === '1') return 1;
    // Explicit 3-digit prefixes that would otherwise match a 2-digit prefix
    const threedigit = ['211', '212', '213', '216', '218', '220', '221', '222', '223', '224', '225', '226', '227', '228', '229', '230', '231', '232', '233', '234', '235', '236', '237', '238', '239', '240', '241', '242', '243', '244', '245', '246', '247', '248', '249', '250', '251', '252', '253', '254', '255', '256', '257', '258', '260', '261', '262', '263', '264', '265', '266', '267', '268', '269', '290', '291', '297', '298', '299', '350', '351', '352', '353', '354', '355', '356', '357', '358', '359', '370', '371', '372', '373', '374', '375', '376', '377', '378', '379', '380', '381', '382', '383', '385', '386', '387', '388', '389', '420', '421', '423', '500', '501', '502', '503', '504', '505', '506', '507', '508', '509', '590', '591', '592', '593', '594', '595', '596', '597', '598', '599', '670', '672', '673', '674', '675', '676', '677', '678', '679', '680', '681', '682', '683', '685', '686', '687', '688', '689', '690', '691', '692', '800', '808', '850', '852', '853', '855', '856', '880', '886', '960', '961', '962', '963', '964', '965', '966', '967', '968', '970', '971', '972', '973', '974', '975', '976', '977', '992', '993', '994', '995', '996', '998'];
    if (threedigit.includes(d.slice(0, 3))) return 3;
    return 2;
  }

  function formatPhoneValue(value) {
    const raw = String(value || '');
    const isExplicitIntl = raw.trimStart().startsWith('+');
    // Strip everything except digits, cap at 15 (E.164 max)
    const digits = raw.replace(/\D/g, '').slice(0, 15);

    if (!digits) return '';

    // US / NANP shorthand: bare 10 digits with no + prefix
    if (!isExplicitIntl && digits.length <= 10) {
      const [a, b, c] = [digits.slice(0, 3), digits.slice(3, 6), digits.slice(6, 10)];
      let out = '+1';
      if (a) out += ` (${a}${a.length === 3 ? ')' : ''}`;
      if (b) out += ` ${b}`;
      if (c) out += `-${c}`;
      return out;
    }

    // International (+ prefix or 11+ raw digits)
    const ccLen = getCountryCodeLength(digits);
    const cc = digits.slice(0, ccLen);
    const local = digits.slice(ccLen);

    // NANP country (+1): keep (NXX) NXX-XXXX style
    if (cc === '1') {
      const [a, b, c] = [local.slice(0, 3), local.slice(3, 6), local.slice(6, 10)];
      let out = '+1';
      if (a) out += ` (${a}${a.length === 3 ? ')' : ''}`;
      if (b) out += ` ${b}`;
      if (c) out += `-${c}`;
      return out;
    }

    // Generic international: +CC XXXX XXXX (groups of 4)
    let out = `+${cc}`;
    if (local) {
      // space-separate into chunks of up to 4
      const chunks = local.match(/.{1,4}/g) || [];
      out += ' ' + chunks.join(' ');
    }
    return out;
  }

  // formatPhone event handler (preserves caret position)
  function formatPhone(e) {
    const input = e.target;
    const prev = input.value;
    const formatted = formatPhoneValue(prev);
    if (formatted !== prev) {
      const caret = input.selectionStart ?? prev.length;
      const digitsBeforeCaret = prev.slice(0, caret).replace(/\D/g, '').length;
      const isLocalShorthand = !prev.trimStart().startsWith('+') && prev.replace(/\D/g, '').length <= 10;
      const targetDigitIndex = digitsBeforeCaret + (isLocalShorthand ? 1 : 0);
      input.value = formatted;
      let d = 0;
      let newCaret = 0;
      for (let i = 0; i < formatted.length; i++) {
        if (/\d/.test(formatted[i])) d++;
        if (d === targetDigitIndex) {
          newCaret = i + 1;
          break;
        }
        newCaret = i + 1;
      }
      input.setSelectionRange(newCaret, newCaret);
    }
    readForm();
  }

  onMount(() => {
    form = document.getElementById(formId);
    if (!form) {
      return;
    }

    form.addEventListener('input', readForm);
    form.addEventListener('change', readForm);
    const phoneInput = form.querySelector('input[name="PhoneNumber"]');
    phoneInput?.addEventListener('input', formatPhone);
    if (phoneInput?.value) {
      phoneInput.value = formatPhoneValue(phoneInput.value);
    }
    readForm();

    return () => {
      form.removeEventListener('input', readForm);
      form.removeEventListener('change', readForm);
      phoneInput?.removeEventListener('input', formatPhone);
    };
  });
</script>

<aside class="mb-6 grid gap-4 md:grid-cols-[1fr_auto]">  
  <div class="rounded-md border border-slate-200 bg-white px-4 py-4 md:min-w-64">
    <p class="text-sm font-semibold text-slate-900">Estado del Relevo de Responsabilidad</p>
    <p class="mt-1 text-sm text-slate-600">
      {waiverSigned ? 'Se requiere firma antes de guardar.' : 'La firma se desbloquea despues de que se marque el relevo de responsabilidad.'}
    </p>
  </div>
</aside>

