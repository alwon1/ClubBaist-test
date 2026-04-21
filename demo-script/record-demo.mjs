/**
 * ClubBaist Video Demo Recording Script
 * Records a single .webm demo video covering 3 sections:
 *   1. Membership Application & Approval
 *   2. Tee Time Bookings & Standing Tee Times
 *   3. Score Entry
 *
 * Usage: node record-demo.mjs
 * Prerequisites: App running at https://localhost:7021 (DB fresh — wiped on restart)
 */

import { chromium } from 'playwright';
import path from 'path';
import { fileURLToPath } from 'url';
import fs from 'fs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const BASE = 'https://localhost:7021';
const VIDEOS_DIR = path.resolve(__dirname, '../videos');

fs.mkdirSync(VIDEOS_DIR, { recursive: true });

async function main() {
  const browser = await chromium.launch({
    headless: false,
    slowMo: 400,
    args: ['--ignore-certificate-errors'],
  });

  const ctx = await browser.newContext({
    recordVideo: { dir: VIDEOS_DIR, size: { width: 1280, height: 720 } },
    ignoreHTTPSErrors: true,
    viewport: { width: 1280, height: 720 },
  });

  const pg = await ctx.newPage();

  // ── helpers ─────────────────────────────────────────────────────────
  const pause = (ms) => pg.waitForTimeout(ms);

  async function blazorReady(timeout = 15000) {
    await pg.waitForFunction(
      () => !document.body.innerText.includes('Loading...'),
      { timeout }
    );
  }

  async function login(email, password) {
    await pg.goto(BASE + '/Account/Login');
    await pg.waitForURL('**/Account/Login');
    await pg.getByRole('textbox', { name: 'Email' }).fill(email);
    await pg.getByRole('textbox', { name: 'Password' }).fill(password);
    await pg.getByRole('button', { name: 'Log in', exact: true }).click();
    await pg.waitForURL(BASE + '/');
    await pause(800);
  }

  async function logout() {
    await pg.getByRole('button', { name: 'Logout' }).click();
    await pg.waitForURL('**/Account/Login');
    await pause(600);
  }

  async function showTitle(title, subtitle = '') {
    await pg.evaluate(({ t, s }) => {
      const existing = document.getElementById('__demo_title__');
      if (existing) existing.remove();
      const el = document.createElement('div');
      el.id = '__demo_title__';
      el.style.cssText = [
        'position:fixed', 'inset:0', 'background:#1a3a2a', 'color:#fff',
        'display:flex', 'flex-direction:column', 'align-items:center',
        'justify-content:center', 'z-index:99999',
        'font-family:system-ui,sans-serif', 'pointer-events:none',
      ].join(';');
      el.innerHTML = `
        <div style="font-size:48px;font-weight:700;margin-bottom:16px;text-align:center;padding:0 60px;line-height:1.2">${t}</div>
        ${s ? `<div style="font-size:26px;opacity:0.75;text-align:center;padding:0 60px">${s}</div>` : ''}
      `;
      document.body.appendChild(el);
    }, { t: title, s: subtitle });
    await pause(subtitle ? 2500 : 3000);
    await pg.evaluate(() => document.getElementById('__demo_title__')?.remove());
    await pause(600);
  }

  // ── INTRO ────────────────────────────────────────────────────────────
  await pg.goto(BASE + '/Account/Login');
  await pg.waitForLoadState('domcontentloaded');
  await showTitle('ClubBaist', 'Club Management System');

  // ════════════════════════════════════════════════════════════════════
  // SECTION 1 — Membership Application & Approval
  // ════════════════════════════════════════════════════════════════════
  await showTitle('Membership Applications', 'Applying and approving new members');

  // --- Submit application as shareholder3 ---
  await login('shareholder3@clubbaist.com', 'Pass@word1');
  await pg.goto(BASE + '/membership/apply');
  await blazorReady();
  await pause(600);

  await pg.getByRole('textbox', { name: 'Email Address' }).fill('sam.tester@example.com');
  await pg.getByRole('textbox', { name: 'First Name' }).fill('Sam');
  await pg.getByRole('textbox', { name: 'Last Name' }).fill('Tester');
  await pg.getByRole('textbox', { name: 'Date of Birth' }).fill('1990-05-15');
  await pg.getByRole('textbox', { name: 'Phone (e.g. (403) 555-1234)' }).fill('(403) 555-1234');
  await pg.getByRole('textbox', { name: 'Address' }).fill('100 Test Street');
  await pg.getByRole('textbox', { name: 'Postal Code (e.g. T2A 4K3)' }).fill('T2A 4K3');
  await pg.getByRole('textbox', { name: 'Occupation' }).fill('Software Developer');
  await pg.getByRole('textbox', { name: 'Company Name' }).fill('Test Corp');
  await pg.getByLabel('Membership Level').selectOption('Bronze');
  await pg.getByRole('spinbutton', { name: 'Sponsor 1 Member ID' }).fill('1');
  await pg.getByRole('spinbutton', { name: 'Sponsor 2 Member ID' }).fill('2');
  await pause(600);
  await pg.getByRole('button', { name: 'Submit Application' }).click();
  await pg.waitForSelector('[role="alert"]', { timeout: 10000 });
  await pause(2200); // let viewer read success alert
  await logout();

  // --- Committee reviews inbox and approves Frank Pending ---
  await login('committee@clubbaist.com', 'Pass@word1');
  await pg.goto(BASE + '/membership/applications');
  await blazorReady();
  await pause(1500); // show inbox with 6 applications

  await pg.getByRole('link', { name: /Frank Pending/i }).click();
  await pg.waitForURL('**/membership/applications/**');
  await blazorReady();
  await pause(1000); // show application detail

  await pg.getByLabel('New Status').selectOption('Accepted');
  await pg.waitForSelector('label:has-text("Membership Level")', { timeout: 8000 });
  await pause(600);
  await pg.getByLabel('Membership Level').selectOption('Associate');
  await pause(500);
  await pg.getByRole('button', { name: 'Submit Decision' }).click();
  await pg.waitForSelector('[role="alert"]', { timeout: 10000 });
  await pause(2200); // read success + Accepted badge
  await logout();

  // ════════════════════════════════════════════════════════════════════
  // SECTION 2 — Tee Time Bookings & Standing Tee Times
  // ════════════════════════════════════════════════════════════════════
  await showTitle('Tee Time Reservations', 'Booking tee times and standing requests');

  await login('shareholder1@clubbaist.com', 'Pass@word1');

  // Show availability grid
  await pg.goto(BASE + '/teetimes');
  await blazorReady();
  await pg.getByRole('textbox', { name: 'Select Date' }).fill('2026-05-09');
  await pg.getByRole('button', { name: 'Refresh' }).click();
  await blazorReady();
  await pause(1500); // show full grid

  // Solo booking May 9 7:00 AM
  await pg.goto(BASE + '/teetimes/book?date=2026-05-09&time=07:00');
  await blazorReady();
  await pause(600);
  await pg.getByRole('button', { name: 'Book Tee Time' }).click();
  await pg.waitForSelector('[role="alert"]', { timeout: 10000 });
  await pause(2000);

  // My Reservations — show booking
  await pg.goto(BASE + '/teetimes/my');
  await blazorReady();
  await pause(1500);

  // Foursome May 10 7:00 AM
  await pg.goto(BASE + '/teetimes/book?date=2026-05-10&time=07:00');
  await blazorReady();
  await pause(600);
  await pg.getByRole('button', { name: '+ Add Player' }).click();
  await pause(400);
  await pg.getByRole('button', { name: '+ Add Player' }).click();
  await pause(400);
  await pg.getByRole('button', { name: '+ Add Player' }).click();
  await pause(500);
  await pg.getByRole('combobox', { name: 'Player 2' }).selectOption('SH-0002');
  await pause(400);
  await pg.getByRole('combobox', { name: 'Player 3' }).selectOption('SH-0003');
  await pause(400);
  await pg.getByRole('combobox', { name: 'Player 4' }).selectOption('SV-0004');
  await pause(500);
  await pg.getByRole('button', { name: 'Book Tee Time' }).click();
  await pg.waitForSelector('[role="alert"]', { timeout: 10000 });
  await pause(2000);

  // Standing tee time request
  await pg.goto(BASE + '/teetimes/standing/request');
  await blazorReady();
  await pause(700);
  await pg.getByLabel('Day of Week *').selectOption('Saturday');
  await pause(400);
  await pg.getByLabel('Requested Time *').fill('08:00');
  await pause(400);
  await pg.getByLabel('Player 2').selectOption('SH-0002 - Bob Shareholder');
  await pause(400);
  await pg.getByLabel('Player 3').selectOption('SH-0003 - Carol Shareholder');
  await pause(400);
  await pg.getByLabel('Player 4').selectOption('SV-0004 - Diana Silver');
  await pause(500);
  await pg.getByRole('button', { name: 'Submit Request' }).click();
  await pg.waitForSelector(':text("submitted and is pending review")', { timeout: 10000 });
  await pause(2200);
  await logout();

  // ════════════════════════════════════════════════════════════════════
  // SECTION 3 — Score Entry
  // ════════════════════════════════════════════════════════════════════
  await showTitle('Scorekeeping', 'Recording a round of golf');

  await login('shareholder1@clubbaist.com', 'Pass@word1');

  // View eligible rounds
  await pg.goto(BASE + '/scores/my');
  await pg.waitForFunction(
    () => !document.body.innerText.includes('Loading...'),
    { timeout: 20000 }
  );
  await pause(1800); // show both eligible bookings

  // Record score — bookingId=1 (today's booking)
  await pg.goto(BASE + '/scores/record?bookingId=1&memberId=1');
  await pg.waitForFunction(
    () => !document.body.innerText.includes('Loading...'),
    { timeout: 20000 }
  );
  await pause(600);

  // Select White tee
  await pg.locator('#tee-White').click();
  await pause(500);

  // Fill all 18 holes with 5 — tab through so running totals update live
  await pg.locator('input[type="number"]').first().click();
  for (let i = 0; i < 18; i++) {
    await pg.keyboard.type('5');
    await pg.keyboard.press('Tab');
    await pause(80);
  }
  // Blur last input to trigger Blazor @onchange
  await pg.locator('h3').first().click();
  await pause(600);

  // Submit
  await pg.getByRole('button', { name: 'Submit Round' }).click();
  await pg.waitForFunction(
    () => !document.body.innerText.includes('Loading...'),
    { timeout: 20000 }
  );
  await pause(2500); // read confirmation: "Score Submitted / 90"

  // Back to My Scores — history row visible
  await pg.goto(BASE + '/scores/my');
  await pg.waitForFunction(
    () => !document.body.innerText.includes('Loading...'),
    { timeout: 20000 }
  );
  await pause(2000);
  await logout();

  // ── END CARD ──────────────────────────────────────────────────────────
  await pg.goto(BASE + '/Account/Login');
  await pg.waitForLoadState('domcontentloaded');
  await showTitle('ClubBaist', 'Managing your club, beautifully.');

  // Finalise — ctx.close() triggers Playwright to save the video file
  await ctx.close();
  const videoPath = await pg.video()?.path();
  await browser.close();

  console.log('\n✅ Recording complete.');
  console.log('Video saved to:', videoPath ?? VIDEOS_DIR);
}

main().catch((err) => {
  console.error('\n❌ Recording failed:', err.message);
  process.exit(1);
});
