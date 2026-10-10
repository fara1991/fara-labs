// サイト全体の演出（Blazor から独立して動く）
//  - スクロール量を CSS 変数 --scroll / --progress に流す（背景のパララックス・上部の進捗バー）
//  - .reveal 要素が画面に入ったら .is-visible を付ける（フェードイン）
//  - .spotlight 要素にマウス位置 --mx / --my を流す（カードの光）
(() => {
    const root = document.documentElement;
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');

    let ticking = false;
    const update = () => {
        const y = window.scrollY;
        const max = root.scrollHeight - window.innerHeight;
        root.style.setProperty('--scroll', reduceMotion.matches ? '0' : String(y));
        root.style.setProperty('--progress', max > 0 ? (y / max).toFixed(4) : '0');
        root.classList.toggle('is-scrolled', y > 8);
        ticking = false;
    };
    const onScroll = () => {
        if (!ticking) {
            ticking = true;
            requestAnimationFrame(update);
        }
    };
    window.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('resize', onScroll, { passive: true });
    update();

    const observer = new IntersectionObserver((entries) => {
        for (const entry of entries) {
            if (entry.isIntersecting) {
                entry.target.classList.add('is-visible');
                observer.unobserve(entry.target);
            }
        }
    }, { threshold: 0.1, rootMargin: '0px 0px -40px 0px' });

    const scan = (node) => {
        if (node.nodeType !== Node.ELEMENT_NODE) return;
        if (node.matches('.reveal:not(.is-visible)')) observer.observe(node);
        node.querySelectorAll('.reveal:not(.is-visible)').forEach((el) => observer.observe(el));
    };
    // Blazor が描画・ページ遷移で追加した要素も自動で監視する
    new MutationObserver((mutations) => {
        for (const m of mutations) m.addedNodes.forEach(scan);
        onScroll();
    }).observe(document.body, { childList: true, subtree: true });
    scan(document.body);
    root.classList.add('js-ready');

    document.addEventListener('pointermove', (e) => {
        const card = e.target instanceof Element ? e.target.closest('.spotlight') : null;
        if (!card) return;
        const rect = card.getBoundingClientRect();
        card.style.setProperty('--mx', `${e.clientX - rect.left}px`);
        card.style.setProperty('--my', `${e.clientY - rect.top}px`);
    }, { passive: true });

    // トップの軌道アイコンの説明（.satellite-tip）が画面の左右からはみ出すときは内側に寄せる
    document.addEventListener('pointerover', (e) => {
        const sat = e.target instanceof Element ? e.target.closest('.satellite') : null;
        const tip = sat?.querySelector('.satellite-tip');
        if (!tip) return;
        sat.classList.remove('tip-left', 'tip-right');
        const rect = tip.getBoundingClientRect();
        const margin = 12;
        if (rect.right > document.documentElement.clientWidth - margin) sat.classList.add('tip-left');
        else if (rect.left < margin) sat.classList.add('tip-right');
    }, { passive: true });

    window.faraSite = {
        scrollToHash() {
            const id = decodeURIComponent(location.hash.slice(1));
            if (!id) return;
            const el = document.getElementById(id);
            if (el) el.scrollIntoView({ behavior: reduceMotion.matches ? 'auto' : 'smooth', block: 'start' });
        },
    };
})();
