/* Orchestra Docs — Navigation & Interactive JS */

(function () {
  'use strict';

  // ── Active nav link ──────────────────────────────────────────
  function setActiveNav() {
    const path = window.location.pathname.replace(/\/$/, '');
    document.querySelectorAll('.navbar-links a, .docs-sidebar a').forEach(function (link) {
      const href = link.getAttribute('href');
      if (!href) return;
      const resolved = new URL(href, window.location.href).pathname.replace(/\/$/, '');
      if (resolved === path) {
        link.classList.add('active');
      }
    });
  }

  // ── Hamburger menu ───────────────────────────────────────────
  function initHamburger() {
    const burger = document.querySelector('.hamburger');
    const links  = document.querySelector('.navbar-links');
    if (!burger || !links) return;
    burger.addEventListener('click', function () {
      links.classList.toggle('open');
    });
    document.addEventListener('click', function (e) {
      if (!burger.contains(e.target) && !links.contains(e.target)) {
        links.classList.remove('open');
      }
    });
  }

  // ── Smooth scroll for anchor links ──────────────────────────
  function initAnchorScroll() {
    document.querySelectorAll('a[href^="#"]').forEach(function (anchor) {
      anchor.addEventListener('click', function (e) {
        const id = this.getAttribute('href').slice(1);
        const target = document.getElementById(id);
        if (target) {
          e.preventDefault();
          const offset = 80;
          const top = target.getBoundingClientRect().top + window.scrollY - offset;
          window.scrollTo({ top: top, behavior: 'smooth' });
        }
      });
    });
  }

  // ── Intersection Observer for sidebar active state ───────────
  function initScrollSpy() {
    const sidebarLinks = document.querySelectorAll('.docs-sidebar a[href^="#"]');
    if (!sidebarLinks.length) return;

    const sections = Array.from(sidebarLinks)
      .map(function (link) {
        const id = link.getAttribute('href').slice(1);
        return document.getElementById(id);
      })
      .filter(Boolean);

    const observer = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          if (entry.isIntersecting) {
            sidebarLinks.forEach(function (link) { link.classList.remove('active'); });
            const active = document.querySelector('.docs-sidebar a[href="#' + entry.target.id + '"]');
            if (active) active.classList.add('active');
          }
        });
      },
      { rootMargin: '-80px 0px -60% 0px', threshold: 0 }
    );

    sections.forEach(function (s) { observer.observe(s); });
  }

  // ── Animate-on-scroll ────────────────────────────────────────
  function initReveal() {
    const els = document.querySelectorAll('[data-reveal]');
    if (!els.length) return;
    const io = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          if (entry.isIntersecting) {
            entry.target.classList.add('animate-fade-in-up');
            io.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.1 }
    );
    els.forEach(function (el) {
      el.style.opacity = '0';
      io.observe(el);
    });
  }

  // ── Copy code blocks ─────────────────────────────────────────
  function initCopyCode() {
    document.querySelectorAll('pre').forEach(function (block) {
      const btn = document.createElement('button');
      btn.textContent = 'Copy';
      btn.style.cssText = [
        'position:absolute', 'top:0.6rem', 'right:0.75rem',
        'background:rgba(99,102,241,0.2)', 'color:rgb(148,163,184)',
        'border:1px solid rgba(30,41,59,1)', 'border-radius:5px',
        'font-size:0.7rem', 'font-weight:600', 'padding:0.25rem 0.6rem',
        'cursor:pointer', 'transition:background 0.2s,color 0.2s'
      ].join(';');
      btn.addEventListener('mouseenter', function () {
        btn.style.background = 'rgba(99,102,241,0.4)';
        btn.style.color = '#fff';
      });
      btn.addEventListener('mouseleave', function () {
        btn.style.background = 'rgba(99,102,241,0.2)';
        btn.style.color = 'rgb(148,163,184)';
      });
      btn.addEventListener('click', function () {
        const code = block.querySelector('code') || block;
        navigator.clipboard.writeText(code.textContent || '').then(function () {
          btn.textContent = 'Copied!';
          btn.style.color = 'rgb(16,185,129)';
          setTimeout(function () {
            btn.textContent = 'Copy';
            btn.style.color = 'rgb(148,163,184)';
          }, 2000);
        });
      });
      block.style.position = 'relative';
      block.appendChild(btn);
    });
  }

  // ── Hero word carousel (for index.html) ──────────────────────
  function initWordCarousel() {
    const container = document.getElementById('word-carousel');
    if (!container) return;
    const words  = ['Design', 'Build', 'Test', 'Deploy'];
    const colors = ['rgb(168,85,247)', 'rgb(34,211,238)', 'rgb(234,179,8)', 'rgb(16,185,129)'];
    let idx = 0;

    function render() {
      container.innerHTML = '';
      const span = document.createElement('span');
      span.textContent = words[idx];
      span.style.color = colors[idx];
      span.style.transition = 'opacity 0.4s, transform 0.4s';
      span.style.display = 'block';
      container.appendChild(span);
    }

    function next() {
      const current = container.querySelector('span');
      if (current) {
        current.style.opacity = '0';
        current.style.transform = 'translateY(-24px)';
      }
      setTimeout(function () {
        idx = (idx + 1) % words.length;
        render();
        const newEl = container.querySelector('span');
        if (newEl) {
          newEl.style.opacity = '0';
          newEl.style.transform = 'translateY(24px)';
          requestAnimationFrame(function () {
            newEl.style.opacity = '1';
            newEl.style.transform = 'translateY(0)';
          });
        }
        // Also update workflow stage
        updateWorkflowStages(idx);
      }, 400);
    }

    render();
    setInterval(next, 2500);
  }

  function updateWorkflowStages(activeIdx) {
    document.querySelectorAll('.workflow-stage').forEach(function (stage, i) {
      stage.classList.remove('active', 'done');
      if (i === activeIdx) stage.classList.add('active');
      else if (i < activeIdx) stage.classList.add('done');
    });
  }

  // ── Init ─────────────────────────────────────────────────────
  document.addEventListener('DOMContentLoaded', function () {
    setActiveNav();
    initHamburger();
    initAnchorScroll();
    initScrollSpy();
    initReveal();
    initCopyCode();
    initWordCarousel();
    updateWorkflowStages(0);
  });
})();
