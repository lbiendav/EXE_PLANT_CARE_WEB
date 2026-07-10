/* ═══════════════════════════════════════════════════════════════
   HOME PLANT — bilingual layer (VI default · EN toggle)
   VI copy lives in the HTML; EN copy lives here. The VI strings are
   captured from the DOM on boot, so there is a single source of truth
   for each language and switching just swaps innerHTML.
   ═══════════════════════════════════════════════════════════════ */
(() => {
  'use strict';

  const STORE_KEY = 'hp-lang';
  const DEFAULT_LANG = 'vi';

  /* English strings, keyed to [data-i18n] attributes in index.html.
     Values may contain inline markup (<em>, <br>, <b>, <span>). */
  const EN = {
    'loader.note': 'an indoor plant studio',

    'head.guide': 'Guide&nbsp;№&nbsp;01&nbsp;—&nbsp;Sanctuary',
    'head.join': 'Join',

    'hero.kicker': 'Indoor&nbsp;Plant&nbsp;Studio&nbsp;·&nbsp;Est.&nbsp;in&nbsp;stillness',
    'hero.sub': 'Sanctuary — our first plant-care guide.<br>Twelve species for busy people.',
    'hero.cue': 'Scroll',
    'hero.inter': 'One plant. Every angle of care.',

    'story.ghost': 'STILLNESS',
    'story.kicker': 'The Studio',
    'story.t1': 'Growing',
    'story.t2': 'in <em>stillness.</em>',
    'story.b1': 'A plant does not hurry.',
    'story.b2': 'It listens — to light, to water,',
    'story.b3': 'to the air of the room.',
    'story.b4': '<em>Sanctuary</em> is a guide to listening back.',
    'story.m1': 'Twelve species',
    'story.m2': 'Four rituals',
    'story.m3': 'One quiet hour a week',

    'macro.kicker': 'The Surface',
    'macro.note': 'closer&nbsp;—&nbsp;until the leaf becomes a landscape',
    'macro.d1': 'Light',
    'macro.d2': 'Water',
    'macro.d3': 'Humidity',
    'macro.s1w': 'Light.',
    'macro.s1c': 'The leaf drinks what the sun spills.<br>Bright — never direct.',
    'macro.s2w': 'Water.',
    'macro.s2c': 'Dew, not flood.<br>Moisture that lingers, never pools.',
    'macro.s3w': 'Humidity.',
    'macro.s3c': 'Air you can almost hold.<br>Sixty to seventy percent.',

    'anat.kicker': 'Every layer, on purpose',
    'anat.title': 'Anatomy of a<br>Potted&nbsp;Plant',
    'anat.c1no': '01 · Vessel',
    'anat.c1h': 'Raw terracotta',
    'anat.c1p': 'Unglazed clay breathes,<br>so the roots do too.',
    'anat.c2no': '02 · Substrate',
    'anat.c2h': 'Three draining layers',
    'anat.c2p': 'Gravel, then grit,<br>then living soil.',
    'anat.c2s': '<b>3-layer</b> draining substrate',
    'anat.c3no': '03 · Roots',
    'anat.c3h': 'Water, on request',
    'anat.c3p': 'Test with a fingertip, not a schedule.',
    'anat.c3s': 'water when the top <b>3&nbsp;cm</b> runs dry',
    'anat.c4no': '04 · Leaves',
    'anat.c4h': 'Borrowed sun',
    'anat.c4p': 'Near the window, never on the sill.',
    'anat.c4s': 'indirect light · <b>6–8&nbsp;h</b>/day',
    'anat.c5no': '05 · Air',
    'anat.c5h': 'A soft climate',
    'anat.c5p': 'Mist, or a humidifier nearby.',
    'anat.c5s': 'humidity <b>60–70%</b>',
    'anat.done': 'Complete. <span>— one Monstera, assembled</span>',

    'ritual.ghost': 'RITUAL',
    'ritual.kicker': 'The Ritual',
    'ritual.t1': 'Understand first,',
    'ritual.t2': 'then <em>tend.</em>',
    'ritual.b1': 'Taken apart to understand — reassembled to care.',
    'ritual.b2': 'Every layer of soil, every root has its reason.',
    'ritual.b3': 'What remains is repetition, kept gentle and even.',
    'ritual.b4': 'And <em>Sanctuary</em> turns repetition into ritual.',
    'ritual.m1': 'Five layers',
    'ritual.m2': 'One potted plant',
    'ritual.m3': 'A weekly ritual',

    'atmos.kicker': 'The Guide',
    'atmos.sub': 'A corner of the room that breathes.',

    'col.kicker': 'The Collection',
    'col.t1': 'Twelve species',
    'col.t2': 'for <em>busy</em> people.',
    'col.note': 'Chosen to forgive. Ranked by how much they ask of you — which is very little.',

    'card1n': 'Monstera',       'card1x': 'Forgives forgetfulness.',        'card1w': 'water · 9&nbsp;days',  'card1l': 'light · medium',
    'card2n': 'ZZ Plant',       'card2x': 'Thrives on neglect.',            'card2w': 'water · 14&nbsp;days', 'card2l': 'light · low',
    'card3n': 'Snake Plant',    'card3x': 'Sleeps standing up.',            'card3w': 'water · 14&nbsp;days', 'card3l': 'light · low',
    'card4n': 'Pothos',         'card4x': 'Grows toward any light it finds.','card4w': 'water · 7&nbsp;days', 'card4l': 'light · low',
    'card5n': 'Heartleaf',      'card5x': 'Soft-hearted, low-effort.',      'card5w': 'water · 7&nbsp;days',  'card5l': 'light · medium',
    'card6n': 'Cast-Iron Plant','card6x': 'The name is a promise.',         'card6w': 'water · 10&nbsp;days', 'card6l': 'light · low',
    'card7n': 'Chinese Evergreen','card7x': 'Patient in dim corners.',      'card7w': 'water · 8&nbsp;days',  'card7l': 'light · low',
    'card8n': 'Dragon Tree',    'card8x': 'Slow, upright, unbothered.',     'card8w': 'water · 12&nbsp;days', 'card8l': 'light · medium',
    'card9n': 'Peace Lily',     'card9x': 'Tells you when it’s thirsty.',   'card9w': 'water · 6&nbsp;days',  'card9l': 'light · medium',
    'card10n': 'Wax Plant',     'card10x': 'Blooms for the patient.',       'card10w': 'water · 10&nbsp;days','card10l': 'light · bright',
    'card11n': 'Rubber Plant',  'card11x': 'Broad leaves, few demands.',    'card11w': 'water · 9&nbsp;days', 'card11l': 'light · medium',
    'card12n': 'Spider Plant',  'card12x': 'Multiplies, quietly.',          'card12w': 'water · 7&nbsp;days', 'card12l': 'light · medium',

    'cta.kicker': 'The Schedule',
    'cta.t1': 'Care,',
    'cta.t2': 'on <em>Sundays.</em>',
    'cta.copy': 'What to water, What to mist,<br>What to leave alone.<br>No worries.',
    'cta.fine': 'Join now for free.',

    'foot.mid': 'Guide № 01 — <em>Sanctuary</em> · twelve species for busy people',
    'foot.right': '© 2026 · made in stillness',
  };

  const nodes = Array.from(document.querySelectorAll('[data-i18n]'));
  // Capture the Vietnamese source (as authored in the HTML) once, up front.
  const VI = new Map();
  nodes.forEach((el) => VI.set(el, el.innerHTML));

  const apply = (lang) => {
    nodes.forEach((el) => {
      const key = el.dataset.i18n;
      if (lang === 'en') {
        if (key in EN) el.innerHTML = EN[key];
      } else {
        el.innerHTML = VI.get(el);
      }
    });
    document.documentElement.lang = lang;
    const btn = document.getElementById('langToggle');
    if (btn) btn.dataset.active = lang;
    try { localStorage.setItem(STORE_KEY, lang); } catch (e) { /* private mode */ }
    window.__lang = lang;
  };

  let saved = DEFAULT_LANG;
  try { saved = localStorage.getItem(STORE_KEY) || DEFAULT_LANG; } catch (e) { /* ignore */ }
  apply(saved);

  const btn = document.getElementById('langToggle');
  if (btn) {
    btn.addEventListener('click', () => {
      apply((window.__lang === 'en') ? 'vi' : 'en');
    });
  }

  // exposed for programmatic control / testing
  window.__setLang = apply;
})();
