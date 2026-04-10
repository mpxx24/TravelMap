// ============================================================
// TravelMap — Interactive world map with country visit tracking
// ============================================================

(function () {
  // Theme: apply immediately to avoid flash
  var theme = localStorage.getItem("theme") || "dark";
  document.documentElement.setAttribute("data-theme", theme);
})();

document.addEventListener("DOMContentLoaded", function () {
  var config = window.TravelMapConfig || {};
  var visits = {}; // countryCode -> CountryVisit
  var geoLayer = null;

  // ---- Continent lookup (ISO alpha-3 -> continent label) ----
  var _continentGroups = {
    "Europe":     ["ALB","AND","AUT","BLR","BEL","BIH","BGR","HRV","CYP","CZE","DNK","EST","FIN","FRA","DEU","GRC","HUN","ISL","IRL","ITA","XKX","LVA","LIE","LTU","LUX","MLT","MDA","MCO","MNE","NLD","MKD","NOR","POL","PRT","ROU","RUS","SMR","SRB","SVK","SVN","ESP","SWE","CHE","UKR","GBR","VAT"],
    "Asia":       ["AFG","ARM","AZE","BHR","BGD","BTN","BRN","KHM","CHN","GEO","IND","IDN","IRN","IRQ","ISR","JPN","JOR","KAZ","KWT","KGZ","LAO","LBN","MYS","MDV","MNG","MMR","NPL","PRK","OMN","PAK","PHL","QAT","SAU","SGP","KOR","LKA","SYR","TWN","TJK","THA","TLS","TUR","TKM","ARE","UZB","VNM","YEM"],
    "Africa":     ["DZA","AGO","BEN","BWA","BFA","BDI","CMR","CPV","CAF","TCD","COM","COD","COG","CIV","DJI","EGY","GNQ","ERI","ETH","GAB","GMB","GHA","GIN","GNB","KEN","LSO","LBR","LBY","MDG","MWI","MLI","MRT","MUS","MAR","MOZ","NAM","NER","NGA","RWA","STP","SEN","SLE","SOM","ZAF","SSD","SDN","SWZ","TZA","TGO","TUN","UGA","ZMB","ZWE"],
    "N. America": ["ATG","BHS","BRB","BLZ","CAN","CRI","CUB","DMA","DOM","SLV","GRD","GTM","HTI","HND","JAM","MEX","NIC","PAN","KNA","LCA","VCT","TTO","USA"],
    "S. America": ["ARG","BOL","BRA","CHL","COL","ECU","GUY","PRY","PER","SUR","URY","VEN"],
    "Oceania":    ["AUS","FJI","KIR","MHL","FSM","NRU","NZL","PLW","PNG","WSM","SLB","TON","TUV","VUT"]
  };
  var CONTINENT_MAP = {};
  Object.keys(_continentGroups).forEach(function (c) {
    _continentGroups[c].forEach(function (code) { CONTINENT_MAP[code] = c; });
  });
  var CONTINENT_ORDER = ["Europe", "Asia", "Africa", "N. America", "S. America", "Oceania"];

  // ---- Country code lookup ----
  // Natural Earth uses "-99" as a sentinel for "no ISO code assigned".
  // Fall back to ADM0_A3 so each country gets a unique, meaningful code.
  function getCode(props) {
    var iso = props.ISO_A3;
    return (iso && iso !== "-99") ? iso : props.ADM0_A3;
  }

  // ---- Color scheme ----
  var COLORS = {
    1: "#28a745", // Mainland - green
    2: "#17a2b8", // Islands - blue
    3: "#ffc107", // Both - gold
  };
  var WISHLIST_COLOR = "#9b59b6"; // purple

  var VISIT_LABELS = {
    1: "Mainland",
    2: "Islands",
    3: "Both",
  };

  // ---- Map initialization ----
  var map = L.map("map", {
    center: [30, 10],
    zoom: 3,
    minZoom: 2,
    maxZoom: 8,
    zoomControl: true,
    worldCopyJump: true,
  });

  // Tile layer — use CartoDB dark/light based on theme
  var darkTiles =
    "https://{s}.basemaps.cartocdn.com/dark_nolabels/{z}/{x}/{y}{r}.png";
  var lightTiles =
    "https://{s}.basemaps.cartocdn.com/light_nolabels/{z}/{x}/{y}{r}.png";
  var currentTheme = localStorage.getItem("theme") || "dark";
  var tileLayer = L.tileLayer(
    currentTheme === "light" ? lightTiles : darkTiles,
    {
      attribution:
        '&copy; <a href="https://www.openstreetmap.org/copyright">OSM</a> &copy; <a href="https://carto.com/">CARTO</a>',
      subdomains: "abcd",
      maxZoom: 19,
    }
  ).addTo(map);

  // ---- Theme toggle ----
  var themeToggle = document.getElementById("theme-toggle");
  var themeIcon = document.getElementById("theme-icon");

  function applyTheme(t) {
    document.documentElement.setAttribute("data-theme", t);
    themeIcon.className = t === "light" ? "bi bi-sun" : "bi bi-moon-stars";
    localStorage.setItem("theme", t);

    // Swap tile layer
    var url = t === "light" ? lightTiles : darkTiles;
    tileLayer.setUrl(url);

    // Re-style GeoJSON borders
    if (geoLayer) {
      geoLayer.eachLayer(function (layer) {
        var code = getCode(layer.feature.properties);
        var visit = visits[code];
        layer.setStyle(getStyle(layer.feature, !!visit, t));
      });
    }
  }

  // Set initial icon
  themeIcon.className =
    currentTheme === "light" ? "bi bi-sun" : "bi bi-moon-stars";

  themeToggle.addEventListener("click", function () {
    var current = localStorage.getItem("theme") || "dark";
    applyTheme(current === "dark" ? "light" : "dark");
  });

  // ---- Country styling ----
  function getStyle(feature, isVisited, themeOverride) {
    var t = themeOverride || localStorage.getItem("theme") || "dark";
    var code = getCode(feature.properties);
    var visit = visits[code];

    if (visit) {
      if (visit.isWishlist) {
        return {
          fillColor: WISHLIST_COLOR,
          fillOpacity: 0.25,
          color: WISHLIST_COLOR,
          weight: 1.5,
          opacity: 0.7,
          dashArray: "4 3",
        };
      }
      return {
        fillColor: COLORS[visit.visitType] || COLORS[1],
        fillOpacity: 0.55,
        color: COLORS[visit.visitType] || COLORS[1],
        weight: 1.5,
        opacity: 0.8,
      };
    }

    return {
      fillColor: "transparent",
      fillOpacity: 0.01,
      color: t === "light" ? "#9ca3af" : "#30363d",
      weight: 0.8,
      opacity: 0.6,
    };
  }

  // ---- Hover + click handlers ----
  function onEachFeature(feature, layer) {
    var props = feature.properties;
    var name = props.NAME || props.ADMIN || "Unknown";
    var code = getCode(props);

    // Tooltip on hover
    layer.bindTooltip(name, {
      sticky: true,
      className: "country-tooltip",
    });

    // Hover highlight
    layer.on("mouseover", function () {
      if (!layer._isPopupOpen) {
        layer.setStyle({ weight: 2.5, opacity: 1 });
        layer.bringToFront();
      }
    });

    layer.on("mouseout", function () {
      if (!layer._isPopupOpen) {
        var visit = visits[code];
        layer.setStyle(getStyle(feature, !!visit));
      }
    });

    // Click: show popup (only when authenticated)
    layer.on("click", function () {
      if (!config.isAuthenticated) return;
      layer._isPopupOpen = true;
      showPopup(layer, code, name);
    });

    layer.on("popupclose", function () {
      layer._isPopupOpen = false;
      var visit = visits[code];
      layer.setStyle(getStyle(feature, !!visit));
    });
  }

  // ---- Popup (edit form) ----
  function showPopup(layer, code, name) {
    var visit = visits[code] || null;
    var isWishlist = visit ? !!visit.isWishlist : false;
    var selectedType = visit && !isWishlist ? visit.visitType : 1;
    var firstVisited = visit && visit.firstVisited
      ? visit.firstVisited.substring(0, 10)
      : "";
    var lastVisited = visit && visit.lastVisited
      ? visit.lastVisited.substring(0, 10)
      : "";
    var notes = visit ? visit.notes || "" : "";

    var visitedStyle = isWishlist ? ' style="display:none"' : "";
    var html =
      '<div class="popup-form">' +
      '<div class="popup-title">' + escapeHtml(name) + "</div>" +
      "<label>Mode</label>" +
      '<div class="radio-group mode-group">' +
      '<div class="radio-btn mode-btn' + (!isWishlist ? " active" : "") + '" data-mode="visited">Visited</div>' +
      '<div class="radio-btn mode-btn' + (isWishlist ? " active mode-wishlist" : "") + '" data-mode="wishlist">Wishlist</div>' +
      "</div>" +
      '<div class="visit-details"' + visitedStyle + '>' +
      "<label>Visit type</label>" +
      '<div class="radio-group">' +
      '<div class="radio-btn' + (selectedType === 1 ? " active" : "") + '" data-value="1">Mainland</div>' +
      '<div class="radio-btn' + (selectedType === 2 ? " active" : "") + '" data-value="2">Islands</div>' +
      '<div class="radio-btn' + (selectedType === 3 ? " active" : "") + '" data-value="3">Both</div>' +
      "</div>" +
      "<label>First visited</label>" +
      '<input type="date" class="popup-first-visited" value="' + firstVisited + '">' +
      "<label>Last visited</label>" +
      '<input type="date" class="popup-last-visited" value="' + lastVisited + '">' +
      "</div>" +
      "<label>Notes</label>" +
      '<textarea class="popup-notes" placeholder="Optional notes...">' + escapeHtml(notes) + "</textarea>" +
      '<div class="popup-actions">' +
      '<button class="btn-save" data-code="' + code + '" data-name="' + escapeHtml(name) + '">Save</button>' +
      (visit
        ? '<button class="btn-remove" data-code="' + code + '">Remove</button>'
        : "") +
      "</div>" +
      (visit ? '<div class="popup-photos"><label>Photos</label><div class="photo-grid" id="photo-grid-' + code + '"></div>' +
        '<div class="photo-upload-row">' +
        '<button class="btn-add-photo">+ Add photo</button>' +
        '<input type="file" class="photo-file-input" accept="image/jpeg,image/png,image/gif,image/webp" style="display:none">' +
        '<span class="photo-upload-status"></span></div></div>' : "") +
      "</div>";

    layer
      .bindPopup(html, {
        maxWidth: 280,
        minWidth: 240,
        closeButton: true,
      })
      .openPopup();

    setTimeout(function () {
      var popup = document.querySelector(".popup-form");
      if (!popup) return;

      // Mode toggle (Visited / Wishlist)
      popup.querySelectorAll(".mode-btn").forEach(function (btn) {
        btn.addEventListener("click", function () {
          popup.querySelectorAll(".mode-btn").forEach(function (b) {
            b.classList.remove("active", "mode-wishlist");
          });
          btn.classList.add("active");
          var wishlistMode = btn.getAttribute("data-mode") === "wishlist";
          if (wishlistMode) btn.classList.add("mode-wishlist");
          var details = popup.querySelector(".visit-details");
          if (details) details.style.display = wishlistMode ? "none" : "";
        });
      });

      // Visit type radio buttons
      popup.querySelectorAll(".radio-group:not(.mode-group) .radio-btn").forEach(function (btn) {
        btn.addEventListener("click", function () {
          popup.querySelectorAll(".radio-group:not(.mode-group) .radio-btn").forEach(function (b) {
            b.classList.remove("active");
          });
          btn.classList.add("active");
        });
      });

      // Save button
      var saveBtn = popup.querySelector(".btn-save");
      if (saveBtn) {
        saveBtn.addEventListener("click", function () {
          var modeBtn = popup.querySelector(".mode-btn.active");
          var wishlist = modeBtn ? modeBtn.getAttribute("data-mode") === "wishlist" : false;
          var activeType = popup.querySelector(".radio-group:not(.mode-group) .radio-btn.active");
          var visitType = activeType
            ? parseInt(activeType.getAttribute("data-value"))
            : 1;

          var visitData = {
            countryCode: code,
            countryName: name,
            isWishlist: wishlist,
            visitType: wishlist ? 0 : visitType,
            firstVisited: wishlist ? null : (popup.querySelector(".popup-first-visited").value || null),
            lastVisited: wishlist ? null : (popup.querySelector(".popup-last-visited").value || null),
            notes: popup.querySelector(".popup-notes").value || null,
          };

          saveVisit(visitData, layer);
        });
      }

      // Remove button
      var removeBtn = popup.querySelector(".btn-remove");
      if (removeBtn) {
        removeBtn.addEventListener("click", function () {
          deleteVisit(code, layer);
        });
      }

      // Photo gallery
      if (visit) {
        renderPhotoGrid(code, popup);

        var addPhotoBtn = popup.querySelector(".btn-add-photo");
        var fileInput = popup.querySelector(".photo-file-input");
        var uploadStatus = popup.querySelector(".photo-upload-status");

        if (addPhotoBtn && fileInput) {
          addPhotoBtn.addEventListener("click", function () { fileInput.click(); });
          fileInput.addEventListener("change", function () {
            var file = fileInput.files[0];
            if (!file) return;
            uploadStatus.textContent = "Uploading…";
            addPhotoBtn.disabled = true;
            var form = new FormData();
            form.append("photo", file);
            fetch("/api/visits/" + encodeURIComponent(code) + "/photos", {
              method: "POST",
              body: form,
            })
              .then(function (res) {
                if (!res.ok) return res.text().then(function (t) { throw new Error(t); });
                return res.json();
              })
              .then(function (data) {
                if (!visits[code]) visits[code] = { countryCode: code, photoIds: [] };
                if (!visits[code].photoIds) visits[code].photoIds = [];
                visits[code].photoIds.push(data.photoId);
                renderPhotoGrid(code, popup);
                uploadStatus.textContent = "";
              })
              .catch(function (err) {
                uploadStatus.textContent = "Upload failed: " + err.message;
              })
              .finally(function () {
                addPhotoBtn.disabled = false;
                fileInput.value = "";
              });
          });
        }
      }
    }, 50);
  }

  function renderPhotoGrid(code, popup) {
    var grid = popup.querySelector("#photo-grid-" + code);
    if (!grid) return;
    var photoIds = (visits[code] && visits[code].photoIds) ? visits[code].photoIds : [];
    grid.innerHTML = photoIds.map(function (id) {
      return '<div class="photo-thumb-wrap">' +
        '<img class="photo-thumb" src="/api/visits/' + encodeURIComponent(code) + '/photos/' + encodeURIComponent(id) + '" loading="lazy" alt="photo" ' +
        'onclick="window.open(this.src,\'_blank\')">' +
        '<button class="photo-delete" data-id="' + escapeHtml(id) + '" title="Delete photo">&times;</button>' +
        '</div>';
    }).join("");

    grid.querySelectorAll(".photo-delete").forEach(function (btn) {
      btn.addEventListener("click", function (e) {
        e.stopPropagation();
        var photoId = btn.getAttribute("data-id");
        fetch("/api/visits/" + encodeURIComponent(code) + "/photos/" + encodeURIComponent(photoId), {
          method: "DELETE",
        })
          .then(function (res) { if (!res.ok) throw new Error(); })
          .then(function () {
            if (visits[code] && visits[code].photoIds) {
              visits[code].photoIds = visits[code].photoIds.filter(function (id) { return id !== photoId; });
            }
            renderPhotoGrid(code, popup);
          })
          .catch(function () { console.error("Delete photo failed"); });
      });
    });
  }

  // ---- API calls ----
  function saveVisit(visitData, layer) {
    fetch("/api/visits", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(visitData),
    })
      .then(function (res) {
        if (!res.ok) throw new Error("Save failed");
        return res.json();
      })
      .then(function (saved) {
        visits[saved.countryCode] = saved;
        layer.setStyle(getStyle(layer.feature, true));
        layer.closePopup();
        updateStats();
      })
      .catch(function (err) {
        console.error("Save error:", err);
      });
  }

  function deleteVisit(code, layer) {
    fetch("/api/visits/" + encodeURIComponent(code), {
      method: "DELETE",
    })
      .then(function (res) {
        if (!res.ok) throw new Error("Delete failed");
        delete visits[code];
        layer.setStyle(getStyle(layer.feature, false));
        layer.closePopup();
        updateStats();
      })
      .catch(function (err) {
        console.error("Delete error:", err);
      });
  }

  function loadVisits() {
    if (config.initialVisits) {
      return Promise.resolve().then(function () {
        visits = {};
        config.initialVisits.forEach(function (v) {
          visits[v.countryCode] = v;
        });
      });
    }

    if (!config.isAuthenticated) return Promise.resolve();

    return fetch("/api/visits")
      .then(function (res) {
        if (!res.ok) throw new Error("Load failed");
        return res.json();
      })
      .then(function (data) {
        visits = {};
        data.forEach(function (v) {
          visits[v.countryCode] = v;
        });
      })
      .catch(function (err) {
        console.error("Load visits error:", err);
      });
  }

  // ---- Share panel ----
  var shareBtn = document.getElementById("share-btn");
  var sharePanel = document.getElementById("share-panel");
  var shareUrlInput = document.getElementById("share-url-input");
  var shareCopyBtn = document.getElementById("share-copy-btn");
  var shareGenerateBtn = document.getElementById("share-generate-btn");
  var shareRevokeBtn = document.getElementById("share-revoke-btn");
  var shareCloseBtn = document.getElementById("share-close-btn");

  if (shareBtn && sharePanel) {
    shareBtn.addEventListener("click", function () {
      sharePanel.hidden = !sharePanel.hidden;
    });

    shareCloseBtn.addEventListener("click", function () {
      sharePanel.hidden = true;
    });

    shareGenerateBtn.addEventListener("click", function () {
      shareGenerateBtn.disabled = true;
      fetch("/api/share", { method: "POST" })
        .then(function (res) { if (!res.ok) throw new Error(); return res.json(); })
        .then(function (data) {
          shareUrlInput.value = data.shareUrl;
          shareCopyBtn.disabled = false;
          shareRevokeBtn.disabled = false;
          shareGenerateBtn.textContent = "Regenerate";
        })
        .catch(function () { console.error("Share generate failed"); })
        .finally(function () { shareGenerateBtn.disabled = false; });
    });

    shareCopyBtn.addEventListener("click", function () {
      navigator.clipboard.writeText(shareUrlInput.value).then(function () {
        var icon = shareCopyBtn.querySelector("i");
        icon.className = "bi bi-clipboard-check";
        setTimeout(function () { icon.className = "bi bi-clipboard"; }, 2000);
      });
    });

    shareRevokeBtn.addEventListener("click", function () {
      if (!confirm("Revoke this share link? Anyone with the link will lose access.")) return;
      fetch("/api/share", { method: "DELETE" })
        .then(function (res) { if (!res.ok) throw new Error(); })
        .then(function () {
          shareUrlInput.value = "";
          shareCopyBtn.disabled = true;
          shareRevokeBtn.disabled = true;
          shareGenerateBtn.textContent = "Generate link";
        })
        .catch(function () { console.error("Share revoke failed"); });
    });
  }

  // ---- Stats ----
  function updateStats() {
    var visitedCodes = Object.keys(visits).filter(function (c) { return !visits[c].isWishlist; });
    var count = visitedCodes.length;
    var total = 195;
    var pct = Math.round((count / total) * 100);
    var el = document.getElementById("legend-stats");
    if (el) el.textContent = count + " / " + total + " countries (" + pct + "%)";

    // Continent breakdown (visited only)
    var breakdown = {};
    visitedCodes.forEach(function (code) {
      var c = CONTINENT_MAP[code] || "Other";
      breakdown[c] = (breakdown[c] || 0) + 1;
    });
    var bd = document.getElementById("legend-breakdown");
    if (bd) {
      bd.innerHTML = CONTINENT_ORDER
        .filter(function (c) { return breakdown[c]; })
        .map(function (c) {
          return '<div class="breakdown-row"><span>' + c + '</span><span>' + breakdown[c] + '</span></div>';
        }).join("");
    }
  }

  // ---- Stats toggle ----
  var legendToggle = document.getElementById("legend-toggle");
  var legendBreakdown = document.getElementById("legend-breakdown");
  if (legendToggle && legendBreakdown) {
    legendToggle.addEventListener("click", function () {
      var expanded = legendToggle.getAttribute("aria-expanded") === "true";
      legendToggle.setAttribute("aria-expanded", String(!expanded));
      legendToggle.innerHTML = (!expanded ? "&#9662;" : "&#9656;") + " by continent";
      legendBreakdown.classList.toggle("breakdown-open", !expanded);
    });
  }

  // ---- Load GeoJSON + visits in parallel ----
  var geojsonPromise = fetch("/data/countries.geojson").then(function (res) {
    return res.json();
  });

  var visitsPromise = loadVisits();

  Promise.all([geojsonPromise, visitsPromise])
    .then(function (results) {
      var geojson = results[0];

      geoLayer = L.geoJSON(geojson, {
        style: function (feature) {
          var code = getCode(feature.properties);
          return getStyle(feature, !!visits[code]);
        },
        onEachFeature: onEachFeature,
      }).addTo(map);

      updateStats();
      hideLoading();
    })
    .catch(function (err) {
      console.error("Map load error:", err);
      hideLoading();
    });

  function hideLoading() {
    var overlay = document.getElementById("map-loading");
    if (!overlay) return;
    overlay.classList.add("fade-out");
    setTimeout(function () { overlay.remove(); }, 350);
  }

  // ---- Utility ----
  function escapeHtml(str) {
    if (!str) return "";
    return str
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }
});
